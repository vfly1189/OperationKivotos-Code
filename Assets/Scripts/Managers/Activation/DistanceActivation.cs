using System.Collections.Generic;
using UnityEngine;

// ⑤ 거리 — 켜기 20m / 끄기 25m. 구역 없이 스포너 중심과 플레이어의 바닥(x·z) 거리로 판정.
// 꺼진 스포너는 켜기 반경 안에 들어와야 켜지고, 켜진 스포너는 끄기 반경 밖으로 나가야 꺼진다 → 사이(20~25m)에선 이전 상태 유지.
public class DistanceActivation : ActivationPolicy
{
    private readonly float _onRadius;
    private readonly float _offRadius;
    private readonly float _onRadiusSqr;
    private readonly float _offRadiusSqr;

    private SpawnerRecord[] _records;

    // ⑥이 창 후보(id)만 판정할 때 쓰는 중심 사전.
    private readonly Dictionary<int, Vector3> _centerById = new Dictionary<int, Vector3>();

    public DistanceActivation(float onRadius, float offRadius)
    {
        _onRadius = onRadius;
        _offRadius = offRadius;
        _onRadiusSqr = onRadius * onRadius;
        _offRadiusSqr = offRadius * offRadius;
    }

    public override void Init(ActivationLayout layout)
    {
        _records = layout._records;
        _centerById.Clear();

        foreach (SpawnerRecord record in _records) _centerById[record._spawnerId] = record._center;
    }

    public override void SelectSpawners(Vector3 playerPos, HashSet<int> current, HashSet<int> result)
    {
        foreach (SpawnerRecord record in _records)
            if (IsWanted(record._spawnerId, record._center, playerPos, current)) result.Add(record._spawnerId);
    }

    public bool IsWanted(int spawnerId, Vector3 playerPos, HashSet<int> current) =>
        _centerById.TryGetValue(spawnerId, out Vector3 center) && IsWanted(spawnerId, center, playerPos, current);

    private bool IsWanted(int spawnerId, Vector3 center, Vector3 playerPos, HashSet<int> current)
    {
        float dx = center.x - playerPos.x;
        float dz = center.z - playerPos.z;
        float radiusSqr = current.Contains(spawnerId) ? _offRadiusSqr : _onRadiusSqr;
        return dx * dx + dz * dz <= radiusSqr;
    }

    public override string ToString() => $"Distance · 켜기 {_onRadius}m / 끄기 {_offRadius}m";

#if UNITY_EDITOR
    public override void DrawGizmos(Vector3 playerPos) => ActivationGizmos.DrawRange(playerPos, _onRadius, _offRadius);
#endif
}
