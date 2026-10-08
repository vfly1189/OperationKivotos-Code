using UnityEngine;

// 비교 후보 7개. 번호 = 03_Comparison_Plan의 ①~⑦.
public enum ActivationPolicyType
{
    Sector = 1,             // ① 서 있는 섹터만
    AdjacentSector = 2,     // ② 섹터 + 인접
    Spotlight3x3 = 3,       // ③ 3×3 · 20m
    Spotlight5x5 = 4,       // ④ 5×5 · 10m
    Distance = 5,           // ⑤ 켜기 20 / 끄기 25m
    SpotlightDistance = 6,  // ⑥ 5×5 · 13m 창 + ⑤
    CullingGroup = 7,       // ⑦ 거리 밴드 20 / 25m
}

// 정책 생성 한 곳. 값은 계획서 0-1 결정 고정표 그대로 — 바꾸면 이전 측정이 무효가 된다.
public static class ActivationPolicies
{
    public const float OnRadius = 20f;
    public const float OffRadius = 25f;
    public static readonly Vector2 ChunkOrigin = new Vector2(-90f, -45f);

    public static ActivationPolicy Create(ActivationPolicyType type) => type switch
    {
        ActivationPolicyType.Sector => new SectorActivation(),
        ActivationPolicyType.AdjacentSector => new AdjacentSectorActivation(),
        ActivationPolicyType.Spotlight3x3 => new SpotlightActivation(3, 20f, ChunkOrigin),
        ActivationPolicyType.Spotlight5x5 => new SpotlightActivation(5, 10f, ChunkOrigin),
        ActivationPolicyType.Distance => new DistanceActivation(OnRadius, OffRadius),
        ActivationPolicyType.SpotlightDistance => new SpotlightDistanceActivation(5, 13f, ChunkOrigin, OnRadius, OffRadius),
        ActivationPolicyType.CullingGroup => new CullingGroupActivation(OnRadius, OffRadius),
        _ => null,
    };

    public static string Label(ActivationPolicyType type) => "①②③④⑤⑥⑦".Substring((int)type - 1, 1);
}
