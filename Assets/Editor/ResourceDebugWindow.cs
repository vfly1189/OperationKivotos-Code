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
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _showGlobal = EditorGUILayout.ToggleLeft($"Global ({globalCount})", _showGlobal, GUILayout.Width(120));
            _showScene = EditorGUILayout.ToggleLeft($"Scene ({sceneCount})", _showScene, GUILayout.Width(120));
            using (new EditorGUI.DisabledScope(!_memoryMeasured))
                _sortBySize = EditorGUILayout.ToggleLeft("크기순 정렬", _sortBySize, GUILayout.Width(100));
        }

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

    // ---- 핸들별 런타임 메모리 추정 (에디터 전용: CollectDependencies) ----
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
            _sizeByKey[entry.Key] = sum;
        }

        _memoryMeasured = true;
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
