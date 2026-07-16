using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

// [Phase 0 계측] 살아있는 Addressables 핸들을 실시간으로 보여주는 디버그 창.
//  - 버킷(Global/Scene)별 핸들 목록 + 어떤 코드가 최초 로드했는지(Source) 표시
//  - [메모리 측정]: 핸들별 의존성(텍스처/메시/오디오 포함) 런타임 메모리 추정치 + 버킷 합계(중복 제거)
//  - Baseline 측정과 이후 Phase 2~3 검증(씬 왕복 후 잔존 핸들·메모리 확인)에 사용
//  - Phase 2에서 내부가 레지스트리로 바뀌어도 이 창은 스냅샷 API만 바라보므로 그대로 유지
public class ResourceDebugWindow : EditorWindow
{
    [MenuItem("Tools/Resource Debug 창")]
    private static void Open()
    {
        GetWindow<ResourceDebugWindow>("Resource Debug");
    }

    private readonly List<ResourceHandleDebugInfo> _buffer = new List<ResourceHandleDebugInfo>();
    private Vector2 _scroll;
    private string _search = "";
    private bool _showGlobal = true;
    private bool _showScene = true;
    private bool _sortBySize = false;

    // ---- 메모리 측정 캐시 ([메모리 측정] 버튼을 누른 시점 기준) ----
    private readonly Dictionary<string, long> _sizeByKey = new Dictionary<string, long>();
    private long _globalBytes;
    private long _sceneBytes;
    private bool _memoryMeasured;

    // 플레이 중 자동 갱신 (OnInspectorUpdate ≈ 10fps, 폴링 부담 없음)
    private void OnInspectorUpdate()
    {
        if (Application.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서만 핸들 상태를 표시합니다.", MessageType.Info);
            _memoryMeasured = false;
            return;
        }

        var rm = Managers.Resource;
        rm.GetHandleSnapshot(_buffer);

        int globalCount = 0, sceneCount = 0;
        foreach (var e in _buffer)
        {
            if (e.Bucket == "Global") globalCount++;
            else sceneCount++;
        }

        // ---- 헤더: 카운트 + 전체 메모리 요약 ----
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            $"Global: {globalCount}    Scene: {sceneCount}    AtlasSpriteCache: {rm.AtlasSpriteCacheCount}",
            EditorStyles.boldLabel);

        // Unity 전체 메모리 지표 (Baseline의 절대 기준점)
        EditorGUILayout.LabelField(
            $"전체 할당: {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())}    " +
            $"예약: {FormatBytes(Profiler.GetTotalReservedMemoryLong())}    " +
            $"텍스처: {FormatBytes((long)Texture.currentTextureMemory)}",
            EditorStyles.miniLabel);

        if (_memoryMeasured)
        {
            EditorGUILayout.LabelField(
                $"핸들 메모리(중복 제거): Global {FormatBytes(_globalBytes)}    Scene {FormatBytes(_sceneBytes)}    " +
                $"합계 {FormatBytes(_globalBytes + _sceneBytes)}",
                EditorStyles.boldLabel);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _search = EditorGUILayout.TextField("검색", _search);
            if (GUILayout.Button("메모리 측정", GUILayout.Width(90)))
                MeasureMemory();
            if (GUILayout.Button("콘솔로 리포트 출력", GUILayout.Width(140)))
            {
                rm.LogAliveReport("디버그 창 수동 출력");
                if (_memoryMeasured) LogMemoryReport();
            }
            if (GUILayout.Button("로드 통계", GUILayout.Width(80)))
                ResourceMetrics.LogLoadStats(); // [Phase 0.5 계측] 키별 로드 횟수·시간 덤프
            if (GUILayout.Button("아틀라스 런타임 검사", GUILayout.Width(140)))
                LogAtlasRuntimeBinding(rm); // [Phase 0.5 계측] 플레이 중 페이지/원본 바인딩 판별
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _showGlobal = EditorGUILayout.ToggleLeft($"Global ({globalCount})", _showGlobal, GUILayout.Width(120));
            _showScene = EditorGUILayout.ToggleLeft($"Scene ({sceneCount})", _showScene, GUILayout.Width(120));
            using (new EditorGUI.DisabledScope(!_memoryMeasured))
                _sortBySize = EditorGUILayout.ToggleLeft("크기순 정렬", _sortBySize, GUILayout.Width(100));
        }

        DrawCheckpointSection();

        EditorGUILayout.Space(2);

        // ---- 목록 ----
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        IEnumerable<ResourceHandleDebugInfo> ordered = (_memoryMeasured && _sortBySize)
            ? _buffer.OrderByDescending(e => _sizeByKey.TryGetValue(e.Key, out var s) ? s : 0)
            : _buffer.OrderBy(e => e.Bucket).ThenBy(e => e.Key);

        foreach (var entry in ordered)
        {
            if (entry.Bucket == "Global" && !_showGlobal) continue;
            if (entry.Bucket == "Scene" && !_showScene) continue;
            if (!string.IsNullOrEmpty(_search) &&
                !entry.Key.ToLowerInvariant().Contains(_search.ToLowerInvariant()) &&
                !entry.Source.ToLowerInvariant().Contains(_search.ToLowerInvariant()))
                continue;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var bucketColor = entry.Bucket == "Global" ? new Color(0.85f, 0.55f, 0.2f) : new Color(0.35f, 0.65f, 0.95f);
                var prev = GUI.color;
                GUI.color = bucketColor;
                EditorGUILayout.LabelField(entry.Bucket == "Global" ? "[G]" : "[S]", EditorStyles.boldLabel, GUILayout.Width(28));
                GUI.color = prev;

                EditorGUILayout.LabelField(entry.Key, GUILayout.MinWidth(150));
                EditorGUILayout.LabelField(entry.TypeName, GUILayout.Width(100));

                if (_memoryMeasured)
                {
                    long size = _sizeByKey.TryGetValue(entry.Key, out var s) ? s : 0;
                    EditorGUILayout.LabelField(FormatBytes(size), GUILayout.Width(70));
                }

                EditorGUILayout.LabelField(entry.IsDone ? "완료" : "로딩중", GUILayout.Width(46));
                EditorGUILayout.LabelField("← " + entry.Source, EditorStyles.miniLabel, GUILayout.MinWidth(140));
            }
        }

        EditorGUILayout.EndScrollView();

        if (_memoryMeasured)
            EditorGUILayout.LabelField(
                "※ 개별 크기 = 해당 에셋 + 의존성(텍스처/메시 등) 합. 아틀라스 등 공유 의존성은 개별 크기에 중복 포함되지만, 버킷 합계에서는 한 번만 계산(Global 우선 귀속).",
                EditorStyles.wordWrappedMiniLabel);
    }

    // ---- [Phase 0.5 계측] 측정 시나리오 체크포인트 ----
    //  버튼 클릭 → 로그 파일(MetricsLogs/metrics_*.log)에 구분선 기록.
    //  Metric 태그 로그는 자동으로 같은 파일에 쌓이므로 콘솔 복사가 불필요하다.
    private bool _showCheckpoints = true;

    private void DrawCheckpointSection()
    {
        EditorGUILayout.Space(2);
        _showCheckpoints = EditorGUILayout.Foldout(_showCheckpoints, "측정 시나리오 체크포인트 (클릭 → 로그 파일에 단계 마킹)", true);
        if (!_showCheckpoints) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("① 진입 직후"))
                ResourceMetrics.Checkpoint("1. GameScene 진입 직후");
            if (GUILayout.Button("② 팝업 첫 오픈 시작"))
                ResourceMetrics.Checkpoint("2. 팝업 5종 첫 오픈 시작");
            if (GUILayout.Button("③ 팝업 재오픈 시작"))
                ResourceMetrics.Checkpoint("3. 같은 팝업 재오픈 시작 (캐시 히트 비교)");
            if (GUILayout.Button("④ 던전 왕복 시작"))
                ResourceMetrics.Checkpoint("4. Boss/Normal 던전 왕복 시작");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("⑤ 측정 종료 (로드 통계 덤프 포함)"))
            {
                ResourceMetrics.LogLoadStats();
                ResourceMetrics.Checkpoint("5. 측정 종료");
            }
            if (GUILayout.Button("로그 폴더 열기", GUILayout.Width(110)))
            {
                System.IO.Directory.CreateDirectory(ResourceMetrics.LogDirectory);
                EditorUtility.RevealInFinder(
                    string.IsNullOrEmpty(ResourceMetrics.CurrentLogFilePath)
                        ? ResourceMetrics.LogDirectory
                        : ResourceMetrics.CurrentLogFilePath);
            }
        }

        if (!string.IsNullOrEmpty(ResourceMetrics.CurrentLogFilePath))
            EditorGUILayout.LabelField($"기록 중: {ResourceMetrics.CurrentLogFilePath}", EditorStyles.miniLabel);

        EditorGUILayout.Space(2);
    }

    // [Phase 0.5 계측] 아틀라스 스프라이트가 플레이 중 실제로 바인딩한 텍스처 확인.
    //  에디트 모드 검사(AtlasAuditTool)는 원본 폴백으로 나왔음 — 플레이 모드의 진실을 판별한다.
    //  [AtlasMetric] 태그라 MetricsLogs 파일에도 자동 기록됨.
    private void LogAtlasRuntimeBinding(ResourceManager rm)
    {
        var map = new Dictionary<Texture2D, List<string>>();
        rm.GetAtlasCacheTextures(map);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[AtlasMetric] 런타임 텍스처 바인딩 — 캐시 스프라이트 {rm.AtlasSpriteCacheCount}개 → 고유 텍스처 {map.Count}장");
        sb.AppendLine("  (텍스처명이 'SpriteAtlasTexture-...'면 페이지 패킹 정상, 개별 이름이면 원본 폴백)");

        foreach (var kv in map.OrderByDescending(k => Profiler.GetRuntimeMemorySizeLong(k.Key)))
        {
            var tex = kv.Key;
            sb.AppendLine($"  {FormatBytes(Profiler.GetRuntimeMemorySizeLong(tex)),10}  {tex.name}: {tex.width}x{tex.height} {tex.format}  ← 스프라이트 {kv.Value.Count}개");
        }

        GameLog.Log(sb.ToString());
    }

    // ---- 핸들별 런타임 메모리 추정 (에디터 전용: CollectDependencies) ----
    //  [Phase 0.5 정정] SpriteAtlas는 CollectDependencies가 "원본 텍스처"까지 세어
    //  실제 런타임(패킹된 DXT5 페이지)보다 크게 과대 계상됨 (Standing: 436MB로 측정됐으나 실제 페이지 64MB).
    //  → SpriteAtlas는 런타임 페이지 텍스처 기준으로 측정하도록 특례 처리.
    private void MeasureMemory()
    {
        _sizeByKey.Clear();
        _globalBytes = 0;
        _sceneBytes = 0;

        // 공유 의존성(아틀라스 텍스처 등)은 버킷 합계에서 한 번만 계산.
        // 스냅샷 순서가 Global → Scene 이므로 공유분은 Global에 귀속됨.
        var counted = new HashSet<Object>();

        foreach (var entry in _buffer)
        {
            if (entry.Asset == null)
            {
                _sizeByKey[entry.Key] = 0;
                continue;
            }

            long sum = 0;

            if (entry.Asset is UnityEngine.U2D.SpriteAtlas atlas)
            {
                sum = MeasureAtlasPages(atlas, counted, entry.Bucket);
            }
            else
            {
                foreach (var dep in EditorUtility.CollectDependencies(new[] { entry.Asset }))
                {
                    if (dep == null || dep is MonoScript) continue;

                    long size = Profiler.GetRuntimeMemorySizeLong(dep);
                    sum += size;

                    if (counted.Add(dep))
                    {
                        if (entry.Bucket == "Global") _globalBytes += size;
                        else _sceneBytes += size;
                    }
                }
            }
            _sizeByKey[entry.Key] = sum;
        }

        _memoryMeasured = true;
    }

    // 아틀라스 실제 비용 = 패킹된 페이지 텍스처 합 (원본 텍스처는 빌드에 포함되지 않음)
    private long MeasureAtlasPages(UnityEngine.U2D.SpriteAtlas atlas, HashSet<Object> counted, string bucket)
    {
        var clones = new Sprite[atlas.spriteCount];
        atlas.GetSprites(clones);

        long sum = 0;
        var pages = new HashSet<Texture2D>();
        foreach (var clone in clones)
        {
            if (clone == null) continue;
            if (clone.texture != null) pages.Add(clone.texture);
            DestroyImmediate(clone); // GetSprites가 만든 임시 클론 정리 (측정 부작용 방지)
        }

        foreach (var page in pages)
        {
            long size = Profiler.GetRuntimeMemorySizeLong(page);
            sum += size;

            if (counted.Add(page))
            {
                if (bucket == "Global") _globalBytes += size;
                else _sceneBytes += size;
            }
        }
        return sum;
    }

    // Baseline 기록용: 버킷 합계 + 상위 15개를 콘솔에 출력
    private void LogMemoryReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[ResourceMemory] 핸들 메모리(중복 제거) — Global: {FormatBytes(_globalBytes)}, Scene: {FormatBytes(_sceneBytes)}, 합계: {FormatBytes(_globalBytes + _sceneBytes)}");
        sb.AppendLine($"  전체 할당: {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())} / 예약: {FormatBytes(Profiler.GetTotalReservedMemoryLong())} / 텍스처: {FormatBytes((long)Texture.currentTextureMemory)}");
        sb.AppendLine("  ---- 상위 15개 (개별 크기 = 의존성 포함) ----");

        foreach (var entry in _buffer
                     .OrderByDescending(e => _sizeByKey.TryGetValue(e.Key, out var s) ? s : 0)
                     .Take(15))
        {
            long size = _sizeByKey.TryGetValue(entry.Key, out var s2) ? s2 : 0;
            sb.AppendLine($"  [{(entry.Bucket == "Global" ? "G" : "S")}] {FormatBytes(size),10}  {entry.Key}  ←  {entry.Source}");
        }

        GameLog.Log(sb.ToString());
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1L << 20) return $"{bytes / 1048576f:F1} MB";
        if (bytes >= 1L << 10) return $"{bytes / 1024f:F0} KB";
        return $"{bytes} B";
    }
}
