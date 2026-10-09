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
//  채택 = ⑤ Distance (2026-10-10, 정책 7개 × 3회 측정 → docs/FieldEntity/Policy_Result.pdf · 03_Comparison_Plan 8절).
//   ⑤⑥⑦은 비용이 같고(살려 두는 몹 수가 같음) ⑤가 가장 단순하다. ①~④는 경계 떨림으로 탈락.
//   나머지 정책은 비교 · 측정(-policy 인자, 그림자)용으로 남긴다.
//  교체 조건: 한 지역에 로드되는 스포너가 수천 개를 넘어 ⑤의 판정(스포너당 약 48ns)이 프레임당 0.1ms를 넘으면
//   ⑥(격자 후보) 또는 ⑦로 구현만 바꾼다 — 판정 결과가 같으므로(2-5 동등성) 나머지 코드는 그대로.
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
