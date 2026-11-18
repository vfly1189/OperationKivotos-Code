using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Hard 보스 = 원본 보스의 Prefab Variant — 메뉴: Tools/Boss/Build Insane Boss Variant
// 코드 0: 바뀌는 것은 전부 "값"이다. Variant의 Overrides 목록 = Hard에서 바뀐 것의 전부.
//
// 테마 "반전" — Normal에서 익힌 공략이 반대로 작동한다
//  외형   몸통 머티리얼 → Hieronymus_Original_Insane
//  ① 평타   1발 즉시 → 경고 0.5초 뒤 낙뢰, 플레이어를 3번 따라옴 (StrikeEvent 지연 옵션 · repeat)
//  ② 렐릭   빨강 · 초록이 뜻하는 안쪽 / 바깥쪽이 뒤바뀜 (Task _ifValue 교환)
//  ③ 번개   바깥 → 안 조여옴 대신 안 → 바깥 → 다시 안 (radii 순서)
//  ④ 장판   1개 → 1초 간격 3개, 플레이어 지나간 자리를 따라감 (repeat · interval)
//  ⑤ 소환   RL → Tank (monsterKey)
//  ⑥ 기믹   악이 먼저 · 흔들림 ↑ · HP 25%에 한 번 더 · 탄이 보스로 되돌아옴
//           (Task 이벤트 교환 · jitter · 선택기 슬롯 · 탄환 Variant = BulletController._returnAfter)
//           ※ 되돌아오기만 코드 옵션 1개(BulletController)가 새로 생겼다 — Tank 때 StrikeEvent 지연 옵션과 같은 방식
//  템포     패턴 사이 2.0초 → 1.5초
//
// 이미 있는 Hieronymus_Boss_Insane.prefab(통째 복사본)을 같은 경로 · 같은 GUID로 덮어쓴다 → Addressable 참조 유지.
public static class BossInsaneVariantBuilder
{
    private const string BossPath    = "Assets/Resources_moved/Prefabs/Monster/Hieronymus_Boss.prefab";
    private const string VariantPath = "Assets/Resources_moved/Prefabs/Monster/Hieronymus_Boss_Insane.prefab";
    private const string InsaneMat   = "Assets/Resources/예로니무스/Hieronymus_Original/Hieronymus_Original_Insane.mat";
    private const string WarningPath = "Assets/Resources_moved/Prefabs/Decal/MissilePoint.prefab";   // Tank 포격 경고와 같은 데칼

    private const string HolyBullet     = "Assets/Resources_moved/Prefabs/Projectiles/BossOrbBullet_Holy.prefab";
    private const string EvilBullet     = "Assets/Resources_moved/Prefabs/Projectiles/BossOrbBullet_Evil.prefab";
    private const string HolyReturnPath = "Assets/Resources_moved/Prefabs/Projectiles/BossOrbBullet_Holy_Return.prefab";
    private const string EvilReturnPath = "Assets/Resources_moved/Prefabs/Projectiles/BossOrbBullet_Evil_Return.prefab";

    private const float ReturnAfter = 2f;    // 초 — 7m/s 감속 기준 약 7m 나갔다가 돌아온다
    private const float ReturnLife  = 6f;    // 나감 2초 + 돌아옴 약 1.5초 + 여유

    private static readonly List<string> _log = new List<string>();

    // 탄환 프리팹의 Variant — 되돌아오기 옵션과 수명만 오버라이드
    private static GameObject MakeReturnBullet(string srcPath, string dstPath)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
        if (src == null) { Debug.LogWarning($"[BossInsaneVariantBuilder] 탄환 없음: {srcPath}"); return null; }

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, scene);
            inst.name = System.IO.Path.GetFileNameWithoutExtension(dstPath);

            var bullet = inst.GetComponent<BulletController>();
            var so = new SerializedObject(bullet);
            so.FindProperty("_returnAfter").floatValue = ReturnAfter;
            so.FindProperty("_lifeTime").floatValue = ReturnLife;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(bullet);

            return PrefabUtility.SaveAsPrefabAsset(inst, dstPath, out bool ok) is GameObject saved && ok ? saved : null;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("Tools/Boss/Build Insane Boss Variant")]
    public static void Build()
    {
        var boss    = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
        var mat     = AssetDatabase.LoadAssetAtPath<Material>(InsaneMat);
        var warning = AssetDatabase.LoadAssetAtPath<GameObject>(WarningPath);
        if (boss == null || mat == null || warning == null)
        {
            Debug.LogError($"[BossInsaneVariantBuilder] 재료 없음 — boss:{boss != null} mat:{mat != null} warning:{warning != null}");
            return;
        }

        _log.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(boss, scene);
            inst.name = "Hieronymus_Boss_Insane";
            Transform r = inst.transform;

            // ── 외형 ──
            var body = Find<SkinnedMeshRenderer>(r, "Hieronymus_Original_Body");
            if (body != null)
            {
                var mats = body.sharedMaterials;
                mats[0] = mat;
                body.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                _log.Add("외형: 몸통 머티리얼 → Insane");
            }

            // ── ① 평타: 경고 → 지연 낙뢰 × 3, 회차마다 플레이어 위치를 다시 구함 ──
            Edit<StrikeEvent>(r, "Events/Skill0/NormalAttackStrike", "① 평타 추격 낙뢰 3연타", so =>
            {
                so.FindProperty("_delay").floatValue = 0.5f;
                so.FindProperty("_warning").objectReferenceValue = warning;
                so.FindProperty("_warningEuler").vector3Value = new Vector3(90f, 0f, 0f);
                so.FindProperty("_emit._repeat").intValue = 3;
                so.FindProperty("_emit._interval").floatValue = 0.4f;
            });

            // ── ② 렐릭: 색 의미 반전 ──
            Edit<PatternTask>(r, "Patterns/Pattern_Skill1/NormalSkill_1/Task.StrikeIn",  "② 렐릭 안쪽 ↔ 바깥쪽 (In)",  so => so.FindProperty("_ifValue").intValue = 1);
            Edit<PatternTask>(r, "Patterns/Pattern_Skill1/NormalSkill_1/Task.StrikeOut", "② 렐릭 안쪽 ↔ 바깥쪽 (Out)", so => so.FindProperty("_ifValue").intValue = 0);

            // ── ③ 번개: 안 → 바깥 ──
            // 첫 링을 2m로 — 중심 1m 안은 처음엔 안전(경고 없이 바로 맞지 않게). 마지막 0.5 = 중심으로 모여 터짐
            Edit<CirclePlacement>(r, "Events/Skill2/StrikeLightning/CirclePlacement", "③ 번개 안 → 바깥 → 다시 안", so =>
            {
                var radii = so.FindProperty("_radii");
                float[] v = { 2f, 4f, 6f, 8f, 6f, 4f, 2f, 0.5f };
                radii.arraySize = v.Length;
                for (int i = 0; i < v.Length; i++) radii.GetArrayElementAtIndex(i).floatValue = v[i];
            });

            // ── ④ 장판: 따라오는 3개 ──
            Edit<FieldEvent>(r, "Events/Skill3/StrikeMagicCircle", "④ 장판 1초 간격 3개", so =>
            {
                so.FindProperty("_emit._repeat").intValue = 3;
                so.FindProperty("_emit._interval").floatValue = 1.0f;
            });

            // ── ⑤ 소환: RL → Tank ──
            Edit<SummonEvent>(r, "Events/Skill4/SummonDriodRL", "⑤ 소환 RL → Tank",
                so => so.FindProperty("_monsterKey").stringValue = "Droid_Helmet_Tank");

            // ── ⑥ 기믹: 악이 먼저 · 흔들림 ↑ · 25%에 한 번 더 ──
            var fanHoly = Find<ProjectileEvent>(r, "Events/Gimmick1/FanHoly");
            var fanEvil = Find<ProjectileEvent>(r, "Events/Gimmick1/FanEvil");
            if (fanHoly != null && fanEvil != null)
            {
                // 첫 마커(Holy 태그)에 악 부채꼴이 나가게 — 타임라인은 Normal과 공유하므로 Task 쪽에서 바꾼다
                Edit<PatternTask>(r, "Patterns/Pattern_Gimmick1/GimmickSkill_1/Task.Holy", "⑥ 기믹 첫 웨이브 = 악", so => SetList(so.FindProperty("_events"), fanEvil));
                Edit<PatternTask>(r, "Patterns/Pattern_Gimmick1/GimmickSkill_1/Task.Evil", "⑥ 기믹 둘째 웨이브 = 성", so => SetList(so.FindProperty("_events"), fanHoly));
            }
            // 돌아오는 탄 — 탄환 프리팹의 Variant(되돌아오기 옵션만 켬). 나갔다가 보스로 되돌아오며 한 번 더 지나간다
            var holyReturn = MakeReturnBullet(HolyBullet, HolyReturnPath);
            var evilReturn = MakeReturnBullet(EvilBullet, EvilReturnPath);
            if (holyReturn != null) Edit<ProjectileEvent>(r, "Events/Gimmick1/FanHoly", "⑥ 성 탄 → 되돌아오는 탄", so => so.FindProperty("_projectile").objectReferenceValue = holyReturn);
            if (evilReturn != null) Edit<ProjectileEvent>(r, "Events/Gimmick1/FanEvil", "⑥ 악 탄 → 되돌아오는 탄", so => so.FindProperty("_projectile").objectReferenceValue = evilReturn);

            Edit<CirclePlacement>(r, "Events/Gimmick1/FanHoly/CirclePlacement", "⑥ 흔들림 4 → 7 (Holy)", so => so.FindProperty("_angleJitter").floatValue = 7f);
            Edit<CirclePlacement>(r, "Events/Gimmick1/FanEvil/CirclePlacement", "⑥ 흔들림 4 → 7 (Evil)", so => so.FindProperty("_angleJitter").floatValue = 7f);

            var selector = inst.GetComponentInChildren<BossMonsterPatternSelector>(true);
            var gimmick  = Find<Pattern>(r, "Patterns/Pattern_Gimmick1");
            if (selector != null && gimmick != null)
            {
                var so = new SerializedObject(selector);
                var list = so.FindProperty("_gimmickPatterns");
                int idx = list.arraySize;
                list.arraySize = idx + 1;
                var slot = list.GetArrayElementAtIndex(idx);
                slot.FindPropertyRelative("_startLine").floatValue = 75f;    // _totalHPLine 300 기준 25%
                slot.FindPropertyRelative("_pattern").objectReferenceValue = gimmick;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(selector);
                _log.Add("⑥ 기믹 슬롯 추가: HP선 75 (25%)");
            }
            else Debug.LogWarning("[BossInsaneVariantBuilder] 선택기 또는 Pattern_Gimmick1 없음 — 25% 슬롯 생략");

            // ── 템포 ──
            Edit<BossMonsterController>(r, null, "템포: 패턴 사이 2.0 → 1.5초",
                so => so.FindProperty("_patternInterval").floatValue = 1.5f);

            PrefabUtility.SaveAsPrefabAsset(inst, VariantPath, out bool ok);
            if (ok)
            {
                Debug.Log($"[BossInsaneVariantBuilder] 완료 → {VariantPath}\n- " + string.Join("\n- ", _log));
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath));
            }
            else Debug.LogError("[BossInsaneVariantBuilder] Variant 저장 실패");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // ── 도우미 ──────────────────────────────────────────────
    // path가 null이면 루트, 아니면 루트 기준 경로. 없으면 이름으로 한 번 더 찾는다.
    private static T Find<T>(Transform root, string path) where T : Component
    {
        Transform t = path == null ? root : root.Find(path);
        if (t == null && path != null)
        {
            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            foreach (var c in root.GetComponentsInChildren<Transform>(true))
                if (c.name == leaf) { t = c; break; }
        }
        if (t == null) { Debug.LogWarning($"[BossInsaneVariantBuilder] 경로 없음: {path}"); return null; }

        T comp = path == null ? t.GetComponentInChildren<T>(true) : t.GetComponent<T>();
        if (comp == null) Debug.LogWarning($"[BossInsaneVariantBuilder] {path}에 {typeof(T).Name} 없음");
        return comp;
    }

    private static void Edit<T>(Transform root, string path, string label, System.Action<SerializedObject> apply) where T : Component
    {
        T comp = Find<T>(root, path);
        if (comp == null) return;

        var so = new SerializedObject(comp);
        apply(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
        _log.Add(label);
    }

    private static void SetList(SerializedProperty list, params Object[] items)
    {
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
