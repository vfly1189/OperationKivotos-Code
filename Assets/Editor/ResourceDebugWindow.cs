using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// [Phase 0 계측] 살아있는 Addressables 핸들을 실시간으로 보여주는 디버그 창.
//  - 버킷(Global/Scene)별 핸들 목록 + 어떤 코드가 최초 로드했는지(Source) 표시
//  - Baseline 측정과 이후 Phase 2~3 검증(씬 왕복 후 잔존 핸들 확인)에 사용
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

        // ---- 헤더: 카운트 요약 ----
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            $"Global: {globalCount}    Scene: {sceneCount}    AtlasSpriteCache: {rm.AtlasSpriteCacheCount}",
            EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _search = EditorGUILayout.TextField("검색", _search);
            if (GUILayout.Button("콘솔로 리포트 출력", GUILayout.Width(140)))
                rm.LogAliveReport("디버그 창 수동 출력");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _showGlobal = EditorGUILayout.ToggleLeft($"Global ({globalCount})", _showGlobal, GUILayout.Width(120));
            _showScene = EditorGUILayout.ToggleLeft($"Scene ({sceneCount})", _showScene, GUILayout.Width(120));
        }

        EditorGUILayout.Space(2);

        // ---- 목록 ----
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (var entry in _buffer.OrderBy(e => e.Bucket).ThenBy(e => e.Key))
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

                EditorGUILayout.LabelField(entry.Key, GUILayout.MinWidth(160));
                EditorGUILayout.LabelField(entry.TypeName, GUILayout.Width(110));
                EditorGUILayout.LabelField(entry.IsDone ? "완료" : "로딩중", GUILayout.Width(46));
                EditorGUILayout.LabelField("← " + entry.Source, EditorStyles.miniLabel, GUILayout.MinWidth(150));
            }
        }

        EditorGUILayout.EndScrollView();
    }
}
