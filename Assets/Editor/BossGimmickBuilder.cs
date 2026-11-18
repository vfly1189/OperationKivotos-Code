using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

// 보스 HP선 기믹 "성 · 악 부채꼴 탄막"을 Hieronymus_Boss 프리팹에 조립한다 — 메뉴: Tools/Boss/Build Gimmick Fan Barrage
// 새 런타임 코드 0: 기존 Pattern · TimelineAction · PatternTask · ProjectileEvent · CirclePlacement를 에셋으로만 엮는다.
//
// 하는 일
//  1. 태그 Event.Fire.Holy / Event.Fire.Evil (parent = Event.Fire) — 없으면 만든다
//  2. 타임라인 BossGimmick_01
//     - 발사 마커가 없으면 Holy / Evil을 번갈아 찍는다 (이미 있으면 건드리지 않음 = 손으로 옮긴 시각 보존)
//     - 대상이 빈 Control 클립을 클립 이름과 같은 프리팹 오브젝트에 연결 (Magic circle Red / Yellow)
//  3. 프리팹
//     Patterns/Pattern_Gimmick1                 Pattern
//       └ GimmickSkill_1                         TimelineAction(BossGimmick_01)
//           ├ Task.Holy                          PatternTask(Event.Fire.Holy) → FanHoly
//           └ Task.Evil                          PatternTask(Event.Fire.Evil) → FanEvil
//     Events/Gimmick1
//       ├ FanHoly  ProjectileEvent(Holy 탄) └ CirclePlacement  정면 180° · 11발
//       └ FanEvil  ProjectileEvent(Evil 탄) └ CirclePlacement  반 칸 어긋난 10발 → 두 웨이브가 서로 틈을 메운다
//     선택기 _gimmickPatterns += { _startLine = 150 (= HP 50%, _totalHPLine 300 기준), Pattern_Gimmick1 }
//
// 다시 실행하면 Pattern_Gimmick1 · Events/Gimmick1을 지우고 새로 만든다(값 조정 후 재생성용).
public static class BossGimmickBuilder
{
    private const string BossPath     = "Assets/Resources_moved/Prefabs/Monster/Hieronymus_Boss.prefab";
    private const string TimelinePath = "Assets/Resources/Timelines/BossTimeline/BossGimmick_01.playable";
    private const string TagDir       = "Assets/Resources_moved/Data/Ability/Tag";
    private const string HolyBullet   = "Assets/Resources_moved/Prefabs/Projectiles/BossOrbBullet_Holy.prefab";
    private const string EvilBullet   = "Assets/Resources_moved/Prefabs/Projectiles/BossOrbBullet_Evil.prefab";

    private const string PatternName = "Pattern_Gimmick1";
    private const string ActionName  = "GimmickSkill_1";
    private const string EventsName  = "Gimmick1";

    // ── 조정값 ──────────────────────────────────────────────
    private const float StartLine = 150f;      // 이 줄 아래로 내려가면 1회

    // 발사 시각(초) — 짝수 번째 Holy, 홀수 번째 Evil. 타임라인 길이 4초 기준
    private static readonly double[] WaveTimes = { 0.8, 1.2, 1.6, 2.0, 2.4, 2.8, 3.2 };

    private const int   HolyCount  = 11;       // 정면 180° 양 끝 포함 → 18° 간격
    private const int   EvilCount  = 10;       // 18° 간격 · 9° 어긋남 → Holy 사이사이
    private const float Jitter     = 4f;       // 점마다 ±도 — 웨이브마다 틈이 조금씩 다르게
    private const float SpawnRadius = 1f;      // 보스 중심에서 몇 m 앞에서 나오나
    private static readonly Vector3 SpawnOffset = new Vector3(0f, 1f, 0f);   // 바닥에 깔리지 않게
    // ────────────────────────────────────────────────────────

    [MenuItem("Tools/Boss/Build Gimmick Fan Barrage")]
    public static void Build()
    {
        var holyTag = EnsureTag("Event.Fire.Holy");
        var evilTag = EnsureTag("Event.Fire.Evil");
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        var holyBullet = AssetDatabase.LoadAssetAtPath<GameObject>(HolyBullet);
        var evilBullet = AssetDatabase.LoadAssetAtPath<GameObject>(EvilBullet);

        if (timeline == null) { Debug.LogError($"[BossGimmickBuilder] 타임라인 없음: {TimelinePath}"); return; }
        if (holyBullet == null || evilBullet == null)
        {
            Debug.LogError("[BossGimmickBuilder] 탄환 프리팹 없음 — Tools/Boss/Build Orb Bullets 먼저");
            return;
        }

        AddWaveMarkers(timeline, holyTag, evilTag);

        GameObject root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            BindControlClips(timeline, root);

            Transform patterns = root.transform.Find("Patterns");
            Transform events   = root.transform.Find("Events");
            if (patterns == null || events == null)
            {
                Debug.LogError("[BossGimmickBuilder] 프리팹에 Patterns / Events 없음");
                return;
            }

            // 다시 실행 대비 — 이전 결과 제거
            DestroyChild(patterns, PatternName);
            DestroyChild(events, EventsName);

            int layer = patterns.gameObject.layer;

            // ── Events ──
            Transform evRoot = NewChild(events, EventsName, layer);
            var fanHoly = MakeFan(evRoot, "FanHoly", holyBullet, HolyCount, -90f, 180f, layer);
            var fanEvil = MakeFan(evRoot, "FanEvil", evilBullet, EvilCount, -81f, 162f, layer);

            // ── Pattern ──
            Transform pGo = NewChild(patterns, PatternName, layer);
            var pattern = pGo.gameObject.AddComponent<Pattern>();

            Transform aGo = NewChild(pGo, ActionName, layer);
            var action = aGo.gameObject.AddComponent<TimelineAction>();

            var taskHoly = MakeTask(aGo, "Task.Holy", holyTag, fanHoly, layer);
            var taskEvil = MakeTask(aGo, "Task.Evil", evilTag, fanEvil, layer);

            var aSo = new SerializedObject(action);
            aSo.FindProperty("_timeline").objectReferenceValue = timeline;
            SetList(aSo.FindProperty("_tasks"), taskHoly, taskEvil);
            aSo.ApplyModifiedPropertiesWithoutUndo();

            var pSo = new SerializedObject(pattern);
            pSo.FindProperty("_coolDown").floatValue = 0f;
            SetList(pSo.FindProperty("_steps"), action);
            pSo.ApplyModifiedPropertiesWithoutUndo();

            // ── 선택기 ──
            var selector = root.GetComponentInChildren<BossMonsterPatternSelector>(true);
            if (selector == null)
            {
                Debug.LogError("[BossGimmickBuilder] BossMonsterPatternSelector 없음");
                return;
            }
            RegisterGimmick(selector, pattern);

            PrefabUtility.SaveAsPrefabAsset(root, BossPath, out bool ok);
            if (ok) Debug.Log($"[BossGimmickBuilder] 완료 — {PatternName} (HP선 {StartLine} 아래 1회) · 웨이브 {WaveTimes.Length}");
            else    Debug.LogError("[BossGimmickBuilder] 프리팹 저장 실패");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }

    // ── 태그 ────────────────────────────────────────────────
    private static GameplayTagSO EnsureTag(string name)
    {
        string path = $"{TagDir}/{name}.asset";
        var tag = AssetDatabase.LoadAssetAtPath<GameplayTagSO>(path);
        if (tag != null) return tag;

        tag = ScriptableObject.CreateInstance<GameplayTagSO>();
        tag.parent = AssetDatabase.LoadAssetAtPath<GameplayTagSO>($"{TagDir}/Event.Fire.asset");
        AssetDatabase.CreateAsset(tag, path);
        Debug.Log($"[BossGimmickBuilder] 태그 생성: {path}");
        return tag;
    }

    // ── 타임라인 ────────────────────────────────────────────
    private static void AddWaveMarkers(TimelineAsset timeline, GameplayTagSO holy, GameplayTagSO evil)
    {
        if (timeline.markerTrack == null) timeline.CreateMarkerTrack();
        MarkerTrack track = timeline.markerTrack;

        bool already = track.GetMarkers().OfType<TagMarker>().Any(m => m.Tag == holy || m.Tag == evil);
        if (already)
        {
            Debug.Log("[BossGimmickBuilder] 발사 마커가 이미 있음 — 시각을 그대로 둔다");
            return;
        }

        for (int i = 0; i < WaveTimes.Length; i++)
        {
            var marker = track.CreateMarker<TagMarker>(WaveTimes[i]);
            if (!AssetDatabase.Contains(marker)) AssetDatabase.AddObjectToAsset(marker, timeline);

            var so = new SerializedObject(marker);
            so.FindProperty("_tag").objectReferenceValue = (i % 2 == 0) ? holy : evil;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline);
    }

    // 대상이 빈 Control 클립 → 클립 이름과 같은 프리팹 오브젝트에 연결 (디렉터의 ExposedReference 표)
    private static void BindControlClips(TimelineAsset timeline, GameObject root)
    {
        var director = root.GetComponentInChildren<PlayableDirector>(true);
        if (director == null) { Debug.LogWarning("[BossGimmickBuilder] PlayableDirector 없음 — Control 연결 생략"); return; }

        foreach (var track in timeline.GetOutputTracks().OfType<ControlTrack>())
        {
            foreach (var clip in track.GetClips())
            {
                if (!(clip.asset is ControlPlayableAsset cpa)) continue;

                var so = new SerializedObject(cpa);
                var nameProp = so.FindProperty("sourceGameObject.exposedName");
                bool bound = !string.IsNullOrEmpty(nameProp.stringValue) &&
                             director.GetReferenceValue(new PropertyName(nameProp.stringValue), out bool valid) != null && valid;
                if (bound) continue;

                GameObject target = FindDeep(root.transform, clip.displayName);
                if (target == null)
                {
                    Debug.LogWarning($"[BossGimmickBuilder] Control 클립 '{clip.displayName}'과 같은 이름의 오브젝트 없음 — 직접 연결 필요");
                    continue;
                }

                string id = Guid.NewGuid().ToString("N");
                nameProp.stringValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
                director.SetReferenceValue(new PropertyName(id), target);
                EditorUtility.SetDirty(cpa);
                Debug.Log($"[BossGimmickBuilder] Control '{clip.displayName}' → {target.name}");
            }
        }
        EditorUtility.SetDirty(timeline);
    }

    // ── 프리팹 조립 ─────────────────────────────────────────
    private static ProjectileEvent MakeFan(Transform parent, string name, GameObject bullet,
                                           int count, float startAngle, float arc, int layer)
    {
        Transform go = NewChild(parent, name, layer);
        var ev = go.gameObject.AddComponent<ProjectileEvent>();

        Transform pGo = NewChild(go, "CirclePlacement", layer);
        var place = pGo.gameObject.AddComponent<CirclePlacement>();

        var ps = new SerializedObject(place);
        ps.FindProperty("_anchor.source").enumValueIndex = (int)AnchorSource.Caster;
        ps.FindProperty("_anchor.key").stringValue = "";
        ps.FindProperty("_anchor.offset").vector3Value = SpawnOffset;
        ps.FindProperty("_anchor.facing").enumValueIndex = (int)FacingSource.TowardTarget;   // 웨이브마다 그 순간 플레이어 쪽
        var radii = ps.FindProperty("_radii");
        radii.arraySize = 1;
        radii.GetArrayElementAtIndex(0).floatValue = SpawnRadius;
        ps.FindProperty("_countPerRing").intValue = count;
        ps.FindProperty("_startAngle").floatValue = startAngle;
        ps.FindProperty("_clockwise").boolValue = true;
        ps.FindProperty("_pointFacing").enumValueIndex = (int)PointFacing.Outward;
        ps.FindProperty("_arc").floatValue = arc;
        ps.FindProperty("_angleJitter").floatValue = Jitter;
        ps.ApplyModifiedPropertiesWithoutUndo();

        var es = new SerializedObject(ev);
        es.FindProperty("_emit._where").objectReferenceValue = place;
        es.FindProperty("_emit._interval").floatValue = 0f;   // 한 웨이브 = 동시 발사
        es.FindProperty("_emit._batch").intValue = 0;
        es.FindProperty("_emit._repeat").intValue = 1;        // 웨이브 간격은 타임라인 마커가
        es.FindProperty("_emit._saveAs").stringValue = "";
        es.FindProperty("_projectile").objectReferenceValue = bullet;
        es.FindProperty("_muzzleVfx").objectReferenceValue = null;
        es.ApplyModifiedPropertiesWithoutUndo();

        return ev;
    }

    private static PatternTask MakeTask(Transform parent, string name, GameplayTagSO tag, PatternEvent ev, int layer)
    {
        Transform go = NewChild(parent, name, layer);
        var task = go.gameObject.AddComponent<PatternTask>();

        var so = new SerializedObject(task);
        so.FindProperty("_tag").objectReferenceValue = tag;
        so.FindProperty("_ifKey").stringValue = "";
        so.FindProperty("_ifValue").intValue = 0;
        SetList(so.FindProperty("_events"), ev);
        so.ApplyModifiedPropertiesWithoutUndo();
        return task;
    }

    private static void RegisterGimmick(BossMonsterPatternSelector selector, Pattern pattern)
    {
        var so = new SerializedObject(selector);
        var list = so.FindProperty("_gimmickPatterns");

        // 지워진 이전 결과(참조가 빈 칸) 정리
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            var p = list.GetArrayElementAtIndex(i).FindPropertyRelative("_pattern");
            if (p.objectReferenceValue == null) list.DeleteArrayElementAtIndex(i);
        }

        int idx = list.arraySize;
        list.arraySize = idx + 1;
        var slot = list.GetArrayElementAtIndex(idx);
        slot.FindPropertyRelative("_startLine").floatValue = StartLine;
        slot.FindPropertyRelative("_pattern").objectReferenceValue = pattern;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── 도우미 ──────────────────────────────────────────────
    private static Transform NewChild(Transform parent, string name, int layer)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    private static GameObject FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    private static void SetList(SerializedProperty list, params Object[] items)
    {
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
