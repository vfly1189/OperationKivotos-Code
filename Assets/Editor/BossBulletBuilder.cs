using System.IO;
using UnityEditor;
using UnityEngine;

// 보스 탄막용 구슬 탄환 프리팹을 만든다 — 메뉴: Tools/Boss/Build Orb Bullets
// 런타임 코드는 새로 없다: 기존 BulletController(IProjectile) · Poolable 그대로, 보이는 것만 새로.
// 보스 색감(성 = 노랑 · 흰색 / 악 = 빨강 · 검정)에 맞춰 두 벌을 만든다 — 같은 구조, 팔레트만 다름.
//
// 구조
//   BossOrbBullet_X  BulletController · SphereCollider(trigger) · Poolable · TrailRenderer(꼬리)
//    ├ Shade         (악만) 파티클 1개 — 가장 큰 검은 그림자 (Alpha). 가산 블렌드로는 검정이 안 나와서 따로
//    ├ Glow          파티클 1개 — 크고 흐린 바깥 빛 (Additive), 맥동
//    └ Core          파티클 1개 — 작고 밝은 심 (Alpha), 바닥이 밝아도 읽히게
//
// 파티클은 Local 공간 + 짧은 수명 루프 → 풀에서 다시 켜져도 이전 생애 잔상이 남지 않는다.
// 꼬리(TrailRenderer)는 BulletController.OnEnable이 Clear, 풀은 꺼진 상태에서 위치를 옮기므로 끌림 없음.
// 다시 실행하면 같은 경로를 덮어쓴다(값 조정 후 재생성용).
public static class BossBulletBuilder
{
    private const string Dir    = "Assets/Resources_moved/Prefabs/Projectiles";
    private const string MatDir = Dir + "/Materials";

    // 원본 머티리얼 — 복사해서 쓰고 원본은 건드리지 않는다
    private const string GlowSrcMat  = "Assets/Resources/Effect/MagicEffect/Magic effects pack/Materials/GlowFree1.mat";
    private const string TrailSrcMat = "Assets/Resources/Materials/Mat_BulletTrail.mat";

    // ── 팔레트 ──────────────────────────────────────────────
    private struct Palette
    {
        public string name;
        public Color core;       // 심
        public Color glow;       // 바깥 빛 (가산)
        public Color trailHead;  // 꼬리 시작
        public Color trailTail;  // 꼬리 끝 (알파는 0으로 덮음)
        public Color shade;      // 검은 그림자 — a = 0이면 만들지 않음
    }

    // 성: 흰 심 + 금빛 후광 + 연노랑 꼬리
    private static readonly Palette Holy = new Palette
    {
        name      = "Holy",
        core      = new Color(1.00f, 1.00f, 0.95f, 1.00f),
        glow      = new Color(1.00f, 0.78f, 0.25f, 0.80f),
        trailHead = new Color(1.00f, 0.95f, 0.70f, 0.75f),
        trailTail = new Color(1.00f, 0.70f, 0.20f, 0.00f),
        shade     = new Color(0f, 0f, 0f, 0f),
    };

    // 악: 붉은 심 + 진홍 빛 + 검은 그림자 + 검붉은 꼬리
    private static readonly Palette Evil = new Palette
    {
        name      = "Evil",
        core      = new Color(1.00f, 0.55f, 0.50f, 1.00f),
        glow      = new Color(0.90f, 0.05f, 0.08f, 0.85f),
        trailHead = new Color(0.85f, 0.05f, 0.08f, 0.80f),
        trailTail = new Color(0.10f, 0.00f, 0.00f, 0.00f),
        shade     = new Color(0.05f, 0.00f, 0.02f, 0.75f),
    };

    // ── 조정값 ──────────────────────────────────────────────
    private const float Speed      = 7f;     // m/s — 피할 수 있는 속도
    private const float LifeTime   = 4f;     // 초 — 사거리 ≈ 28m
    private const float HitRadius  = 0.3f;   // 판정은 빛보다 작게 (억울하지 않게)
    private const float CoreSize   = 0.45f;
    private const float GlowSize   = 1.2f;
    private const float ShadeSize  = 1.6f;
    private const float TrailTime  = 0.18f;
    private const float TrailWidth = 0.35f;
    // ────────────────────────────────────────────────────────

    [MenuItem("Tools/Boss/Build Orb Bullets")]
    public static void BuildAll()
    {
        if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);

        // 머티리얼은 흰색 · 블렌드만 정하고 색은 파티클 · 트레일이 입힌다 → 두 벌이 같은 머티리얼을 공유
        Material alpha    = MakeParticleMat(MatDir + "/Mat_BossOrb_Alpha.mat",    GlowSrcMat,  additive: false);
        Material additive = MakeParticleMat(MatDir + "/Mat_BossOrb_Additive.mat", GlowSrcMat,  additive: true);
        Material trailAdd = MakeParticleMat(MatDir + "/Mat_BossOrb_TrailAdd.mat", TrailSrcMat, additive: true);
        Material trailAlp = MakeParticleMat(MatDir + "/Mat_BossOrb_TrailAlpha.mat", TrailSrcMat, additive: false);
        if (alpha == null || additive == null || trailAdd == null || trailAlp == null) return;

        // 성 = 빛나는 꼬리(가산), 악 = 어두운 꼬리가 보여야 하므로 알파
        Build(Holy, alpha, additive, trailAdd);
        Build(Evil, alpha, additive, trailAlp);
        AssetDatabase.SaveAssets();
    }

    private static void Build(Palette pal, Material alpha, Material additive, Material trail)
    {
        string prefabPath = $"{Dir}/BossOrbBullet_{pal.name}.prefab";
        var root = new GameObject($"BossOrbBullet_{pal.name}");
        try
        {
            // 판정 · 이동 · 풀 — 기존 탄환과 같은 조합
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = HitRadius;

            var bullet = root.AddComponent<BulletController>();
            var so = new SerializedObject(bullet);
            so.FindProperty("_speed").floatValue = Speed;
            so.FindProperty("_lifeTime").floatValue = LifeTime;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<Poolable>();

            AddTrail(root, trail, pal);

            // sortingFudge가 클수록 뒤에 그려진다: Shade(맨 뒤) → Glow → Core(맨 앞)
            if (pal.shade.a > 0f)
                AddOrb(root.transform, "Shade", alpha, ShadeSize, pal.shade, pulse: 0.10f, sortingFudge: 2f);
            AddOrb(root.transform, "Glow", additive, GlowSize, pal.glow, pulse: 0.15f, sortingFudge: 1f);
            AddOrb(root.transform, "Core", alpha, CoreSize, pal.core, pulse: 0.08f, sortingFudge: 0f);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
            if (ok)
            {
                Debug.Log($"[BossBulletBuilder] 생성: {prefabPath}");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            }
            else Debug.LogError("[BossBulletBuilder] 프리팹 저장 실패");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 파티클 1개를 계속 유지하는 구슬. 루프 길이 = 수명이라 항상 1개, 크기만 맥동.
    private static void AddOrb(Transform parent, string name, Material mat, float size, Color color,
                               float pulse, float sortingFudge)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        const float cycle = 0.5f;

        var main = ps.main;
        main.duration        = cycle;
        main.loop            = true;
        main.playOnAwake     = true;
        main.startLifetime   = cycle + 0.02f;   // 다음 회차와 살짝 겹쳐 깜빡임 없음
        main.startSpeed      = 0f;
        main.startSize       = size;
        main.startColor      = color;
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles    = 2;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode     = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // 맥동: 1 → (1-pulse) → 1
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.5f, 1f - pulse), new Keyframe(1f, 1f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode        = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial    = mat;
        r.sortingFudge      = sortingFudge;   // Core가 Glow 위에 그려지게
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows    = false;
    }

    private static void AddTrail(GameObject root, Material mat, Palette pal)
    {
        var t = root.AddComponent<TrailRenderer>();
        t.sharedMaterial    = mat;
        t.time              = TrailTime;
        t.minVertexDistance = 0.05f;
        t.widthMultiplier   = TrailWidth;
        t.widthCurve        = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.receiveShadows    = false;
        t.alignment         = LineAlignment.View;

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(pal.trailHead, 0f), new GradientColorKey(pal.trailTail, 1f) },
            new[] { new GradientAlphaKey(pal.trailHead.a, 0f), new GradientAlphaKey(0f, 1f) });
        t.colorGradient = g;
    }

    // URP Particles/Unlit 머티리얼을 복사해 블렌드만 정한다.
    // Additive = 바깥 빛 · 꼬리, Alpha = 심(밝은 바닥 위에서도 형태가 보이게)
    private static Material MakeParticleMat(string path, string srcPath, bool additive)
    {
        var src = AssetDatabase.LoadAssetAtPath<Material>(srcPath);
        if (src == null)
        {
            Debug.LogError($"[BossBulletBuilder] 원본 머티리얼 없음: {srcPath}");
            return null;
        }

        var mat = new Material(src) { name = Path.GetFileNameWithoutExtension(path) };

        mat.SetFloat("_Surface", 1f);                       // Transparent
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (additive)
        {
            mat.SetFloat("_Blend", 2f);                     // Additive
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        }
        else
        {
            mat.SetFloat("_Blend", 0f);                     // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");

        // 소프트 파티클 · 카메라 페이드 끔 — 바닥 가까이 날아도 사라지지 않게
        mat.SetFloat("_SoftParticlesEnabled", 0f);
        mat.DisableKeyword("_SOFTPARTICLES_ON");
        mat.SetFloat("_CameraFadingEnabled", 0f);
        mat.DisableKeyword("_FADING_ON");

        mat.SetColor("_BaseColor", Color.white);            // 색은 파티클 · 트레일 쪽에서

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.CopyPropertiesFromMaterial(mat);
            existing.shaderKeywords = mat.shaderKeywords;
            existing.renderQueue = mat.renderQueue;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mat);
            return existing;
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
