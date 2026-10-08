using System.Collections.Generic;
using UnityEngine;

// ⑥ 스포트라이트 + 거리 — 5×5 · 13m 창은 후보 찾기만, 판정은 ⑤의 원(켜기 20 / 끄기 25m).
// 결과는 ⑤와 같고(P6), 판정하는 스포너가 전체가 아니라 창 안 후보뿐이라 선택 비용만 다르다.
// 조건: (창 크기 − 1) / 2 · 칸 ≥ 끄기 반경 (2 · 13 = 26 ≥ 25). 끄기 원이 창 밖으로 나가면 ⑤보다 일찍 꺼진다.
public class SpotlightDistanceActivation : ActivationPolicy
{
    private readonly SpotlightActivation _window;
    private readonly DistanceActivation _distance;

    // 이번 프레임 창 안 후보. 칸이 그대로면 SpotlightActivation이 캐시한 목록을 그대로 다시 채운다.
    private readonly HashSet<int> _candidates = new HashSet<int>();

    public SpotlightDistanceActivation(int windowSize, float cellSize, Vector2 origin, float onRadius, float offRadius)
    {
        if ((windowSize - 1) / 2 * cellSize < offRadius)
            GameLog.LogError($"[SpotlightDistanceActivation] 창 보장 반경 {(windowSize - 1) / 2 * cellSize}m < 끄기 반경 {offRadius}m — ⑤와 결과가 달라진다");

        _window = new SpotlightActivation(windowSize, cellSize, origin);
        _distance = new DistanceActivation(onRadius, offRadius);
    }

    public override void Init(ActivationLayout layout)
    {
        _window.Init(layout);
        _distance.Init(layout);
        _candidates.Clear();
    }

    public override void SelectSpawners(Vector3 playerPos, HashSet<int> current, HashSet<int> result)
    {
        _candidates.Clear();
        _window.SelectSpawners(playerPos, current, _candidates);

        foreach (int id in _candidates)
            if (_distance.IsWanted(id, playerPos, current)) result.Add(id);
    }

    public override string ToString() => $"SpotlightDistance · 창 [{_window}] · 판정 [{_distance}]";

#if UNITY_EDITOR
    // 창(후보) 위에 ⑤의 원(판정). 끄기 원이 창 안에 들어와 있어야 ⑤와 같다.
    public override void DrawGizmos(Vector3 playerPos)
    {
        _window.DrawGizmos(playerPos);
        _distance.DrawGizmos(playerPos);
    }
#endif
}
