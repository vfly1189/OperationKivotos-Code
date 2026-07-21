using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

// [Phase 0 계측] 살아있는 Addressables 핸들을 실시간으로 보여주는 디버그 창.
//  - 버킷(수명 스코프)별 핸들 목록 + 어떤 코드가 최초 로드했는지(Source) 표시
//    [Phase 3] 버킷은 Global/Scene 고정이 아니라 살아있는 스코프에서 동적으로 나온다.
//    소유 스코프가 없는 고아는 "?" 버킷(빨강)으로 표시 = Dispose 훅 누락 검출용
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
    private bool _sortBySize = false;

    // [Phase 3] 버킷이 Global/Scene 2개로 고정이 아니게 됨 (Party 등 수명 스코프 신설).
    //  하드코딩하면 새 스코프가 Scene으로 오분류되어 "Scene 불변 = 회귀 없음" 지표를 오염시킨다.
    private readonly Dictionary<string, int> _countByBucket = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _scopeCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, bool> _showBucket = new Dictionary<string, bool>();

    // ---- 메모리 측정 캐시 ([메모리 측정] 버튼을 누른 시점 기준) ----
    private readonly Dictionary<string, long> _sizeByKey = new Dictionary<string, long>();
    private readonly Dictionary<string, long> _bytesByBucket = new Dictionary<string, long>();
    private bool _memoryMeasured;

    private long TotalBytes()
    {
        long sum = 0;
        foreach (var kv in _bytesByBucket) sum += kv.Value;
        return sum;
    }

    private void AddBucketBytes(string bucket, long size)
    {
        _bytesByBucket.TryGetValue(bucket, out long cur);
        _bytesByBucket[bucket] = cur + size;
    }

    // 버킷별 축약 라벨/색 — 알 수 없는 버킷도 첫 글자로 자동 대응
    private static string ShortLabel(string bucket) =>
        string.IsNullOrEmpty(bucket) ? "[?]" : $"[{char.ToUpperInvariant(bucket[0])}]";

    private static Color BucketColor(string bucket)
    {
        switch (bucket)
        {
            case "Global": return new Color(0.85f, 0.55f, 0.20f);
            case "Scene": return new Color(0.35f, 0.65f, 0.95f);
            case "Party": return new Color(0.45f, 0.85f, 0.45f);
            default: return new Color(0.90f, 0.35f, 0.35f);   // 소유 스코프 없음("?") = 고아 = 경고색
        }
    }

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

        _countByBucket.Clear();
        foreach (var e in _buffer)
        {
            _countByBucket.TryGetValue(e.Bucket, out int c);
            _countByBucket[e.Bucket] = c + 1;
        }

        // ---- 헤더: 스코프 소유 카운트 + 전체 메모리 요약 ----
        //  주의: 헤더는 **스코프가 실제로 쥔 수**, 아래 목록의 버킷 라벨은 **ResolveBucket이 고른 대표 스코프**다.
        //  한 키를 두 스코프가 동시에 소유하면(예: Global 프리로드 + Party 로드) 목록엔 Global로만 보이므로
        //  둘이 어긋나는 것 자체가 "중복 소유가 있다"는 신호 — 라벨 다이어트의 진행도를 여기서 읽는다.
        rm.GetScopeCounts(_scopeCounts);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            string.Join("    ", _scopeCounts.Select(kv => $"{kv.Key}: {kv.Value}")) +
            $"    AtlasSpriteCache: {rm.AtlasSpriteCacheCount}",
            EditorStyles.boldLabel);

        int listed = 0;
        foreach (var kv in _countByBucket) listed += kv.Value;
        int owned = 0;
        foreach (var kv in _scopeCounts) owned += kv.Value;
        if (owned != listed)
            EditorGUILayout.LabelField(
                $"※ 스코프 소유 합 {owned} ≠ 목록 {listed} → 중복 소유 {owned - listed}건 (같은 키를 여러 스코프가 쥠)",
                EditorStyles.miniLabel);

        // Unity 전체 메모리 지표 (Baseline의 절대 기준점)
        EditorGUILayout.LabelField(
            $"전체 할당: {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())}    " +
            $"예약: {FormatBytes(Profiler.GetTotalReservedMemoryLong())}    " +
            $"텍스처: {FormatBytes((long)Texture.currentTextureMemory)}",
            EditorStyles.miniLabel);

        if (_memoryMeasured)
        {
            EditorGUILayout.LabelField(
                "핸들 메모리(중복 제거): " +
                string.Join("    ", _bytesByBucket.Select(kv => $"{kv.Key} {FormatBytes(kv.Value)}")) +
                $"    합계 {FormatBytes(TotalBytes())}",
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
                MeasureMemory(); // [Phase 0.5] 측정 안 하고 눌러도 메모리 리포트가 빠지지 않도록 항상 측정
                LogMemoryReport();
            }
            if (GUILayout.Button("로드 통계", GUILayout.Width(80)))
                ResourceMetrics.LogLoadStats(); // [Phase 0.5 계측] 키별 로드 횟수·시간 덤프
            if (GUILayout.Button("아틀라스 런타임 검사", GUILayout.Width(140)))
                LogAtlasRuntimeBinding(rm); // [Phase 0.5 계측] 플레이 중 페이지/원본 바인딩 판별
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (var kv in _countByBucket)
            {
                if (!_showBucket.ContainsKey(kv.Key)) _showBucket[kv.Key] = true;
                _showBucket[kv.Key] = EditorGUILayout.ToggleLeft(
                    $"{kv.Key} ({kv.Value})", _showBucket[kv.Key], GUILayout.Width(120));
            }
            using (new EditorGUI.DisabledScope(!_memoryMeasured))
                _sortBySize = EditorGUILayout.ToggleLeft("크기순 정렬", _sortBySize, GUILayout.Width(100));
        }

        DrawCheckpointSection(rm);

        EditorGUILayout.Space(2);

        // ---- 목록 ----
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        IEnumerable<ResourceHandleDebugInfo> ordered = (_memoryMeasured && _sortBySize)
            ? _buffer.OrderByDescending(e => _sizeByKey.TryGetValue(e.Key, out var s) ? s : 0)
            : _buffer.OrderBy(e => e.Bucket).ThenBy(e => e.Key);

        foreach (var entry in ordered)
        {
            if (_showBucket.TryGetValue(entry.Bucket, out bool show) && !show) continue;
            if (!string.IsNullOrEmpty(_search) &&
                !entry.Key.ToLowerInvariant().Contains(_search.ToLowerInvariant()) &&
                !entry.Source.ToLowerInvariant().Contains(_search.ToLowerInvariant()))
                continue;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var prev = GUI.color;
                GUI.color = BucketColor(entry.Bucket);
                EditorGUILayout.LabelField(ShortLabel(entry.Bucket), EditorStyles.boldLabel, GUILayout.Width(28));
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

    private void DrawCheckpointSection(ResourceManager rm)
    {
        EditorGUILayout.Space(2);
        _showCheckpoints = EditorGUILayout.Foldout(_showCheckpoints, "측정 시나리오 체크포인트 (클릭 → 단계 마킹 + 메모리 리포트 자동)", true);
        if (!_showCheckpoints) return;

        // 체크포인트 버튼 = 단계 마킹 + 그 시점의 메모리 측정·리포트까지 한 번에 (별도 버튼 불필요)
        void Mark(string label)
        {
            ResourceMetrics.Checkpoint(label);
            MeasureMemory();
            LogMemoryReport();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("① 진입 직후"))
                Mark("1. GameScene 진입 직후");
            if (GUILayout.Button("② 팝업 첫 오픈 시작"))
                Mark("2. 팝업 5종 첫 오픈 시작");
            if (GUILayout.Button("③ 팝업 재오픈 시작"))
                Mark("3. 같은 팝업 재오픈 시작 (캐시 히트 비교)");
            if (GUILayout.Button("④ 던전 왕복 시작"))
                Mark("4. Boss/Normal 던전 왕복 시작");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("⑤ 측정 종료 (로드 통계 덤프 포함)"))
            {
                ResourceMetrics.LogLoadStats();
                Mark("5. 측정 종료");
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
        _bytesByBucket.Clear();

        // 공유 의존성(아틀라스 텍스처 등)은 버킷 합계에서 한 번만 계산.
        // 스냅샷 순서가 Global → 나머지 이므로 공유분은 Global에 귀속됨.
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

                    if (counted.Add(dep)) AddBucketBytes(entry.Bucket, size);
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

            if (counted.Add(page)) AddBucketBytes(bucket, size);
        }
        return sum;
    }

    // Baseline 기록용: 버킷 합계 + 상위 15개를 콘솔에 출력
    private void LogMemoryReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[ResourceMemory] 핸들 메모리(중복 제거) — " +
                      string.Join(", ", _bytesByBucket.Select(kv => $"{kv.Key}: {FormatBytes(kv.Value)}")) +
                      $", 합계: {FormatBytes(TotalBytes())}");
        sb.AppendLine($"  전체 할당: {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())} / 예약: {FormatBytes(Profiler.GetTotalReservedMemoryLong())} / 텍스처: {FormatBytes((long)Texture.currentTextureMemory)}");
        sb.AppendLine("  ---- 상위 15개 (개별 크기 = 의존성 포함) ----");

        foreach (var entry in _buffer
                     .OrderByDescending(e => _sizeByKey.TryGetValue(e.Key, out var s) ? s : 0)
                     .Take(15))
        {
            long size = _sizeByKey.TryGetValue(entry.Key, out var s2) ? s2 : 0;
            sb.AppendLine($"  {ShortLabel(entry.Bucket)} {FormatBytes(size),10}  {entry.Key}  ←  {entry.Source}");
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
