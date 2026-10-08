#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

// 측정 하네스 경로 (계획서 0-5) — 원본 · 변형 맵 공통(두 맵에서 위치가 같은 스포너만 쓴다). 좌표는 x · z.
//  ⓐ 팔 순회: 예측(Predictions.pdf) 경로 그대로 — 통로 x = −5를 따라 각 팔 끝까지 왕복, 655m.
//  돌아오는 길(통로를 따라 북쪽으로):
//  ⓑ 끌기 ×2: 통로 옆 스포너(1018 · S4, 1005 · S2)에 어그로 → 1.5m/s로 섹터 경계를 넘어 남쪽으로 → 15초 대기(리쉬 귀환 · 유예 회수)
//     RL(2m/s · 감지 15m)이 따라오도록 몹보다 느리게. 끝점은 스포너 중심에서 25m(끄기 반경) 밖이고,
//     따라온 몹이 대기 중에 교전을 끝낼 만큼 멀게 — 몹은 플레이어 9.5m 앞에 서서 쏘므로 끝점이 집에서 약 35m 안이면
//     감지(15m) · 리쉬(25m) 둘 다 안 걸려 대기 내내 교전 중으로 남는다(v1의 1005 끝점 (−5, 33) = 27m가 그랬다).
//  ⓒ 왕복 ×8: 1m를 10번 왕복 — 섹터 경계 4(통로) · ③④ 칸 경계 3(x = 50 · 10, 원점 −90 기준) · ⑤ 켜기 반경 20m 1(1010)
//  구간 이름은 계측 events의 segment 이벤트로 남는다(move = 구간 사이 이동, 분석에서 묶어 본다).
//  경로를 바꾸면 이전 측정과 비교할 수 없다 — Version을 올린다.
public static class HarnessRoute
{
    public const int Version = 2;   // v2: ⓑ2 끝점 (−5, 33) → (−5, 18)

    public const float RunSpeed = 5f;          // 캐릭터 이동 속도와 같음
    public const float DragSpeed = 1.5f;       // ⓑ — RL 2m/s보다 느리게 (사격 중 멈추는 시간 포함)
    public const float SettleSeconds = 5f;     // 시작점 순간이동 뒤 첫 스폰이 가라앉을 때까지
    public const float LeashWaitSeconds = 15f; // ⓑ 끝 — 리쉬 귀환 · 유예 회수 대기
    public const float AggroTimeout = 5f;      // ⓑ 시작 — 어그로 확인 최대 대기
    public const float WiggleHalf = 0.5f;      // ⓒ — 경계 ±0.5m = 1m
    public const int WiggleCount = 10;

    public static readonly Vector2 Start = new Vector2(68f, 93f);

    public enum LegType { Walk, Wait, WaitAggro, Segment }

    public struct Leg
    {
        public LegType Type;
        public Vector2 To;
        public float Speed;
        public float Seconds;
        public int SpawnerId;
        public string Label;
    }

    // ⓐ — 예측 시뮬레이션의 웨이포인트(CX = −5)
    private static readonly Vector2[] Sweep =
    {
        new Vector2(68, 93), new Vector2(-5, 93), new Vector2(-5, 55), new Vector2(-70, 50), new Vector2(-5, 48),
        new Vector2(-5, 22), new Vector2(66, 21), new Vector2(-5, 21), new Vector2(-5, 0), new Vector2(-80, -3),
        new Vector2(-5, -3), new Vector2(-5, -22), new Vector2(51, -22),
    };

    public static List<Leg> Build()
    {
        var legs = new List<Leg>();

        Segment(legs, "a_sweep");
        for (int i = 1; i < Sweep.Length; i++) Walk(legs, Sweep[i], RunSpeed);

        Move(legs, new Vector2(50, -22));
        Wiggle(legs, "c1_chunk_x50_S5", new Vector2(50, -22), Vector2.right);

        Move(legs, new Vector2(-5, -22), new Vector2(-5, -11.4f));
        Wiggle(legs, "c2_sector_S4S5", new Vector2(-5, -11.4f), Vector2.up);

        Move(legs, new Vector2(-5, 2), new Vector2(-10, 2));
        Drag(legs, "b1_drag_1018", 1018, new Vector2(-5, 2), new Vector2(-5, -22));

        Move(legs, new Vector2(-5, 14.6f));
        Wiggle(legs, "c3_sector_S3S4", new Vector2(-5, 14.6f), Vector2.up);

        Move(legs, new Vector2(-5, 42));
        Wiggle(legs, "c4_sector_S2S3", new Vector2(-5, 42), Vector2.up);

        Move(legs, new Vector2(-5, 47), new Vector2(-14, 47));
        Drag(legs, "b2_drag_1005", 1005, new Vector2(-5, 47), new Vector2(-5, 18));   // 1005 중심 (−28.6, 46.6)에서 37.1m

        Move(legs, new Vector2(-5, 55.5f));
        Wiggle(legs, "c5_radius20_1010", new Vector2(-5, 55.5f), Vector2.up);   // 1010 중심 (−5.2, 75.5)에서 20.0m

        Move(legs, new Vector2(-5, 71.7f));
        Wiggle(legs, "c6_sector_S1S2", new Vector2(-5, 71.7f), Vector2.up);

        Move(legs, new Vector2(-5, 93), new Vector2(10, 93));
        Wiggle(legs, "c7_chunk_x10_S1", new Vector2(10, 93), Vector2.right);

        Move(legs, new Vector2(50, 93));
        Wiggle(legs, "c8_chunk_x50_S1", new Vector2(50, 93), Vector2.right);

        Segment(legs, "end");
        return legs;
    }

    // 씬 뷰 확인용 — 지나는 점 전부 (왕복은 중심 한 점으로 보인다)
    public static List<Vector2> Points()
    {
        var points = new List<Vector2> { Start };
        foreach (Leg leg in Build())
            if (leg.Type == LegType.Walk) points.Add(leg.To);
        return points;
    }

    private static void Segment(List<Leg> legs, string label) =>
        legs.Add(new Leg { Type = LegType.Segment, Label = label });

    private static void Walk(List<Leg> legs, Vector2 to, float speed) =>
        legs.Add(new Leg { Type = LegType.Walk, To = to, Speed = speed });

    private static void Move(List<Leg> legs, params Vector2[] points)
    {
        Segment(legs, "move");
        foreach (Vector2 p in points) Walk(legs, p, RunSpeed);
    }

    // 중심에 도착한 상태에서 부른다. 끝나면 중심으로 돌아온다.
    private static void Wiggle(List<Leg> legs, string label, Vector2 center, Vector2 axis)
    {
        Segment(legs, label);
        for (int i = 0; i < WiggleCount; i++)
        {
            Walk(legs, center + axis * WiggleHalf, RunSpeed);
            Walk(legs, center - axis * WiggleHalf, RunSpeed);
        }
        Walk(legs, center, RunSpeed);
    }

    private static void Drag(List<Leg> legs, string label, int spawnerId, params Vector2[] points)
    {
        Segment(legs, label);
        legs.Add(new Leg { Type = LegType.WaitAggro, SpawnerId = spawnerId, Seconds = AggroTimeout });
        foreach (Vector2 p in points) Walk(legs, p, DragSpeed);
        legs.Add(new Leg { Type = LegType.Wait, Seconds = LeashWaitSeconds });
    }
}
#endif
