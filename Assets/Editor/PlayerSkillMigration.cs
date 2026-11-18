using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 플레이어 스킬을 AbilitySystem(AbilityData · EffectData)에서 SkillSystem(Pattern · Task · Event)으로 옮긴다.
// 메뉴: Tools/Player/Migrate Abilities to SkillSystem — 다시 실행하면 이전 결과(Skills · 역할 E 프리팹)를 지우고 새로 만든다.
//
// 하는 일
//  1. 비트 태그 Event.Beat0 ~ — 애니 이벤트 AbilityBeat(intParameter = 비트 번호)를 SkillSystem 신호로 바꿀 때 쓴다
//  2. 역할 E — 일단 비움 (2026-11-19 버프 · 가드 · 브레이커 E 제외)
//  3. 캐릭터 프리팹 12개 — SkillSystem 컴포넌트 + 자식 Skills
//       Skills/Attack · Skills/Q   Pattern → Signal(끊길 때까지 비트를 받음) → Beat0 · Beat1 … → Event들
//     BaseCharacter._attackPattern · _qPattern · _eDefaultPattern · _ePatternOverride · _beatTags 연결 (Q · E는 원본이 없으면 빈 패턴)
//
// Effect → Event 대응 (값은 그대로 옮긴다)
//   SpawnProjectiles   → ProjectileEvent + PointPlacement(총구 · 몸통 방향), 연사 = PointEmitter 반복 · 간격
//   SpreadProjectiles  → ProjectileEvent + CirclePlacement(반지름 0 · 펼칠 각도 = 산탄 각)
//   SpawnVFX           → VfxEvent + PointPlacement (attach = 총구 소켓에 붙임)
//   RepeatEffect       → 자식 Event들의 PointEmitter 반복 · 간격
//   DelayEffect · 순서 → 뒤 Event의 _startDelay (예전 Effect 목록은 앞이 끝나야 다음이었다)
//   ApplyStatModifier  → 옮기지 않음 (E 제외와 함께 뺌)
public static class PlayerSkillMigration
{
    private const string CharDir        = "Assets/Resources_moved/Prefabs/Characters";
    private const string TagDir         = "Assets/Resources_moved/Data/Ability/Tag";
    private const string SkillsName     = "Skills";
    private const string Muzzle         = "Muzzle";

    private static StringBuilder _log;

    [MenuItem("Tools/Player/Migrate Abilities to SkillSystem")]
    public static void Migrate()
    {
        _log = new StringBuilder("[PlayerSkillMigration]\n");

        // ── 2. 역할 E — 일단 비움 (2026-11-19: 버프 · 가드 · 브레이커 E 제외). RoleDataSO.ePattern에 패턴을 꽂으면 다시 동작한다

        // ── 3. 캐릭터 ──
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CharDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<BaseCharacter>(true) == null) continue;
            if (MigrateCharacter(path)) n++;
        }

        AssetDatabase.SaveAssets();
        _log.AppendLine($"완료 — 캐릭터 {n}개");
        Debug.Log(_log.ToString());
    }

    // ── 캐릭터 ──────────────────────────────────────────────
    private static bool MigrateCharacter(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var bc = root.GetComponentInChildren<BaseCharacter>(true);
            var so = new SerializedObject(bc);

            // 원본: _abilities (슬롯 · 어빌리티 — 같은 슬롯 안 등장 순서 = 비트 번호), _eAbilityOverride
            var bySlot = new Dictionary<int, List<AbilityData>>();
            SerializedProperty list = so.FindProperty("_abilities");
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                var ability = e.FindPropertyRelative("Ability").objectReferenceValue as AbilityData;
                if (ability == null) continue;
                int slot = e.FindPropertyRelative("Slot").intValue;
                if (!bySlot.TryGetValue(slot, out var l)) bySlot[slot] = l = new List<AbilityData>();
                l.Add(ability);
            }
            var eOverride = so.FindProperty("_eAbilityOverride").objectReferenceValue as AbilityData;
            if (eOverride == null && bySlot.TryGetValue((int)CharacterAbilitySlot.E_Skill, out var eList) && eList.Count > 0)
                eOverride = eList[0];

            GameObject host = bc.gameObject;
            if (host.GetComponent<SkillSystem>() == null) host.AddComponent<SkillSystem>();

            Transform old = host.transform.Find(SkillsName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform skills = NewChild(host.transform, SkillsName, host.layer);

            int maxBeats = 1;
            foreach (var l in bySlot.Values) maxBeats = Mathf.Max(maxBeats, l.Count);

            Pattern attack = bySlot.TryGetValue((int)CharacterAbilitySlot.BaseAttack, out var a)
                ? BuildPattern(NewChild(skills, "Attack", host.layer).gameObject, a, true, null) : null;
            // Q · E는 모든 캐릭터에 — 원본 어빌리티가 없으면 빈 패턴(쿨 · 애니 · 컷신만, 비트를 받을 Task는 나중에 추가)
            Pattern q = BuildPattern(NewChild(skills, "Q", host.layer).gameObject,
                                     bySlot.TryGetValue((int)CharacterAbilitySlot.Q_Skill, out var qa) ? qa : new List<AbilityData>(), true, null);
            Pattern e0 = eOverride != null
                ? BuildPattern(NewChild(skills, "E_Override", host.layer).gameObject, new List<AbilityData> { eOverride }, false, BeatTag(0)) : null;
            Pattern eDefault = BuildPattern(NewChild(skills, "E", host.layer).gameObject, new List<AbilityData>(), true, BeatTag(0));

            so.FindProperty("_attackPattern").objectReferenceValue = attack;
            so.FindProperty("_qPattern").objectReferenceValue = q;
            so.FindProperty("_ePatternOverride").objectReferenceValue = e0;
            so.FindProperty("_eDefaultPattern").objectReferenceValue = eDefault;
            SerializedProperty beats = so.FindProperty("_beatTags");
            beats.arraySize = maxBeats;
            for (int i = 0; i < maxBeats; i++) beats.GetArrayElementAtIndex(i).objectReferenceValue = BeatTag(i);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            _log.AppendLine($"  {System.IO.Path.GetFileNameWithoutExtension(path)} : 평타 {(attack ? "O" : "-")} · Q {(qa != null ? "O" : "빈 패턴")} · E {(e0 ? "전용" : "빈 패턴")}{(ok ? "" : "  ← 저장 실패")}");
            return ok;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 패턴 = Signal 한 단계 + 비트마다 Task ───────────────
    private static Pattern BuildPattern(GameObject go, List<AbilityData> beats, bool hold, GameplayTagSO fireOnBegin)
    {
        var pattern = go.AddComponent<Pattern>();
        Transform aGo = NewChild(go.transform, "Signal", go.layer);
        var action = aGo.gameObject.AddComponent<SignalAction>();

        var tasks = new List<Object>();
        for (int i = 0; i < beats.Count; i++)
        {
            Transform tGo = NewChild(aGo, $"Beat{i} · {beats[i].name}", go.layer);
            var task = tGo.gameObject.AddComponent<PatternTask>();
            List<Object> events = BuildEvents(tGo, beats[i]);

            var ts = new SerializedObject(task);
            ts.FindProperty("_tag").objectReferenceValue = BeatTag(i);
            ts.FindProperty("_ifKey").stringValue = "";
            ts.FindProperty("_ifValue").intValue = 0;
            SetList(ts.FindProperty("_events"), events);
            ts.ApplyModifiedPropertiesWithoutUndo();
            tasks.Add(task);
        }

        var aSo = new SerializedObject(action);
        aSo.FindProperty("_fireOnBegin").objectReferenceValue = fireOnBegin;
        aSo.FindProperty("_holdUntilCancel").boolValue = hold;
        SetList(aSo.FindProperty("_tasks"), tasks);
        aSo.ApplyModifiedPropertiesWithoutUndo();

        var pSo = new SerializedObject(pattern);
        pSo.FindProperty("_coolDown").floatValue = 0f;     // 플레이어 쿨은 캐릭터 데이터 → SkillSystem.TryCommit
        pSo.FindProperty("_faceTarget").boolValue = false;
        SetList(pSo.FindProperty("_steps"), new List<Object> { action });
        pSo.ApplyModifiedPropertiesWithoutUndo();
        return pattern;
    }

    // ── Effect 목록 → Event들 ───────────────────────────────
    private static List<Object> BuildEvents(Transform parent, AbilityData ability)
    {
        var events = new List<Object>();
        float t = ability.CastTime;   // 이 Effect가 시작하는 시각 (예전 목록은 앞 Effect가 끝나야 다음)

        var so = new SerializedObject(ability);
        SerializedProperty list = so.FindProperty("_effects");
        for (int i = 0; i < list.arraySize; i++)
        {
            var effect = list.GetArrayElementAtIndex(i).objectReferenceValue as EffectData;
            if (effect != null) t += Emit(parent, effect, t, 1, 0f, events);
        }
        return events;
    }

    // 반환 = 이 Effect가 예전 목록에서 기다리게 하던 시간
    private static float Emit(Transform parent, EffectData effect, float start, int outerRepeat, float outerInterval, List<Object> events)
    {
        var es = new SerializedObject(effect);
        switch (effect)
        {
            case SpawnProjectiles _:
            {
                int count = Mathf.Max(1, es.FindProperty("_count").intValue);
                float interval = es.FindProperty("_interval").floatValue;
                var ev = MakeProjectile(parent, effect.name, es.FindProperty("_bulletPrefab").objectReferenceValue,
                                        out GameObject go, start);
                AddPoint(go, AnchorSource.Socket, Muzzle, Vector3.zero, FacingSource.CasterForward);
                SetEmit(ev, go.GetComponent<Placement>(), outerRepeat * count, count > 1 ? interval : outerInterval);
                events.Add(ev);
                return outerRepeat == 1 ? (count - 1) * interval : 0f;
            }

            case SpreadProjectiles _:
            {
                int count = Mathf.Max(1, es.FindProperty("_count").intValue);
                float spread = es.FindProperty("_spreadAngle").floatValue;
                var ev = MakeProjectile(parent, effect.name, es.FindProperty("_bulletPrefab").objectReferenceValue,
                                        out GameObject go, start);
                var circle = go.AddComponent<CirclePlacement>();
                var cs = new SerializedObject(circle);
                SetAnchor(cs, AnchorSource.Socket, Muzzle, Vector3.zero, FacingSource.CasterForward);
                SerializedProperty radii = cs.FindProperty("_radii");
                radii.arraySize = 1;
                radii.GetArrayElementAtIndex(0).floatValue = 0f;          // 총구 한 점에서
                cs.FindProperty("_countPerRing").intValue = count;
                cs.FindProperty("_startAngle").floatValue = -spread * 0.5f;
                cs.FindProperty("_arc").floatValue = spread;              // 양 끝 포함 균등
                cs.FindProperty("_clockwise").boolValue = true;
                cs.FindProperty("_pointFacing").enumValueIndex = (int)PointFacing.Outward;
                cs.FindProperty("_angleJitter").floatValue = 0f;
                cs.ApplyModifiedPropertiesWithoutUndo();
                SetEmit(ev, circle, outerRepeat, outerInterval, batch: 0);   // 한 번에 전부
                events.Add(ev);
                return 0f;
            }

            case SpawnVFX _:
            {
                Transform go = NewChild(parent, effect.name, parent.gameObject.layer);
                var ev = go.gameObject.AddComponent<VfxEvent>();

                int src = es.FindProperty("_placement._source").enumValueIndex;   // Muzzle · CasterRoot · TargetPoint · Named · CasterForward
                string named = es.FindProperty("_placement._namedId").stringValue;
                float fwd = es.FindProperty("_placement._forwardDistance").floatValue;
                Vector3 offset = es.FindProperty("_placement._localOffset").vector3Value;
                Vector3 euler = es.FindProperty("_placement._localEuler").vector3Value;
                bool casterRot = es.FindProperty("_placement._useCasterRotation").boolValue;
                bool attach = es.FindProperty("_attach").boolValue;

                AnchorSource source = AnchorSource.Socket;
                string key = Muzzle;
                switch (src)
                {
                    case 1: source = AnchorSource.Caster; key = ""; break;
                    case 2: source = AnchorSource.Caster; key = ""; offset += Vector3.forward; break;     // 예전 TargetPoint = 앞 1m
                    case 3: key = named; break;
                    case 4: source = AnchorSource.Caster; key = ""; offset += Vector3.forward * fwd; break;
                }
                if (euler != Vector3.zero) _log.AppendLine($"  ! {effect.name}: _localEuler {euler} 는 옮기지 않음 — 확인 필요");

                AddPoint(go.gameObject, source, key, offset, casterRot ? FacingSource.CasterForward : FacingSource.AnchorForward);

                var vs = new SerializedObject(ev);
                vs.FindProperty("_vfx").objectReferenceValue = es.FindProperty("_vfxPrefab").objectReferenceValue;
                vs.FindProperty("_attachTo").stringValue = attach && source == AnchorSource.Socket ? key : "";
                vs.FindProperty("_startDelay").floatValue = start;
                vs.ApplyModifiedPropertiesWithoutUndo();
                SetEmit(ev, go.GetComponent<Placement>(), outerRepeat, outerInterval);
                events.Add(ev);
                return 0f;
            }

            case DelayEffect _:
                return es.FindProperty("_seconds").floatValue;

            case RepeatEffect _:
            {
                int count = Mathf.Max(1, es.FindProperty("_count").intValue);
                float interval = es.FindProperty("_interval").floatValue;
                SerializedProperty children = es.FindProperty("_children");
                for (int i = 0; i < children.arraySize; i++)
                {
                    var child = children.GetArrayElementAtIndex(i).objectReferenceValue as EffectData;
                    if (child != null) Emit(parent, child, start, outerRepeat * count, interval, events);   // 회차마다 함께
                }
                return outerRepeat == 1 ? (count - 1) * interval : 0f;
            }

            default:
                _log.AppendLine($"  ! {effect.name} ({effect.GetType().Name}) 는 대응 Event가 없어 건너뜀");
                return 0f;
        }
    }

    // ── 조립 도우미 ─────────────────────────────────────────
    private static ProjectileEvent MakeProjectile(Transform parent, string name, Object bullet, out GameObject go, float start)
    {
        go = NewChild(parent, name, parent.gameObject.layer).gameObject;
        var ev = go.AddComponent<ProjectileEvent>();
        var s = new SerializedObject(ev);
        s.FindProperty("_projectile").objectReferenceValue = bullet;
        s.FindProperty("_muzzleVfx").objectReferenceValue = null;
        s.FindProperty("_packPerShot").boolValue = true;     // 예전처럼 탄마다 치명타
        s.FindProperty("_startDelay").floatValue = start;
        s.ApplyModifiedPropertiesWithoutUndo();
        return ev;
    }

    private static void AddPoint(GameObject go, AnchorSource source, string key, Vector3 offset, FacingSource facing)
    {
        var point = go.AddComponent<PointPlacement>();
        var ps = new SerializedObject(point);
        SetAnchor(ps, source, key, offset, facing);
        ps.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetAnchor(SerializedObject so, AnchorSource source, string key, Vector3 offset, FacingSource facing)
    {
        so.FindProperty("_anchor.source").enumValueIndex = (int)source;
        so.FindProperty("_anchor.key").stringValue = key;
        so.FindProperty("_anchor.offset").vector3Value = offset;
        so.FindProperty("_anchor.facing").enumValueIndex = (int)facing;
    }

    private static void SetEmit(PatternEvent ev, Placement where, int repeat, float interval, int batch = 1)
    {
        var s = new SerializedObject(ev);
        s.FindProperty("_emit._where").objectReferenceValue = where;
        s.FindProperty("_emit._repeat").intValue = Mathf.Max(1, repeat);
        s.FindProperty("_emit._interval").floatValue = repeat > 1 ? interval : 0f;
        s.FindProperty("_emit._batch").intValue = batch;
        s.FindProperty("_emit._saveAs").stringValue = "";
        s.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameplayTagSO BeatTag(int i)
    {
        string path = $"{TagDir}/Event.Beat{i}.asset";
        var tag = AssetDatabase.LoadAssetAtPath<GameplayTagSO>(path);
        if (tag != null) return tag;

        tag = ScriptableObject.CreateInstance<GameplayTagSO>();
        AssetDatabase.CreateAsset(tag, path);
        _log.AppendLine($"  태그 생성: {path}");
        return tag;
    }

    private static Transform NewChild(Transform parent, string name, int layer)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void SetList(SerializedProperty prop, List<Object> items)
    {
        prop.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
