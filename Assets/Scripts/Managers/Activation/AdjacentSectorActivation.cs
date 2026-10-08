using System.Collections.Generic;
using UnityEngine;


public class AdjacentSectorActivation : ActivationPolicy
{
    private Sector[] _sectors;

    // 배열 순서는 섹터 ID 순서가 아니다(프리팹 저장 순서) → ID로 찾는 사전을 따로 둔다.
    private readonly Dictionary<int, Sector> _sectorById = new Dictionary<int, Sector>();

    // 섹터 ID → 이웃 섹터 ID들
    private readonly Dictionary<int, int[]> _adjacent = new Dictionary<int, int[]>
    {
        { 1, new[] { 2 } },
        { 2, new[] { 1, 3 } },
        { 3, new[] { 2, 4 } },
        { 4, new[] { 3, 5 } },
        { 5, new[] { 4 } },
    };

    public override void Init(ActivationLayout layout)
    {
        _sectors = layout._sectors;
        _sectorById.Clear();

        foreach (Sector sector in _sectors)
        {
            sector.Init();
            _sectorById[sector._sectorID] = sector;
        }
    }

    public override void SelectSpawners(Vector3 playerPos, HashSet<int> current, HashSet<int> result)
    {
        foreach (Sector sector in _sectors)
        {
            if (sector == null || !sector.Contains(playerPos)) continue;

            // 서 있는 섹터
            AddSpawners(sector, result);

            // 이웃 섹터 (겹침 구간에선 서 있는 섹터가 둘이라 이웃도 합집합으로 들어간다)
            if (!_adjacent.TryGetValue(sector._sectorID, out int[] neighbors)) continue;
            foreach (int neighborId in neighbors)
            {
                if (_sectorById.TryGetValue(neighborId, out Sector neighbor) && neighbor != null)
                    AddSpawners(neighbor, result);
            }
        }
    }

    private static void AddSpawners(Sector sector, HashSet<int> result)
    {
        foreach (int id in sector.GetSpawnersID()) result.Add(id);
    }

#if UNITY_EDITOR
    // 서 있는 섹터는 노랑, 그 이웃은 하늘색
    public override void DrawGizmos(Vector3 playerPos)
    {
        foreach (Sector sector in _sectors)
        {
            if (sector == null || !sector.Contains(playerPos)) continue;
            if (!_adjacent.TryGetValue(sector._sectorID, out int[] neighbors)) continue;

            foreach (int neighborId in neighbors)
                if (_sectorById.TryGetValue(neighborId, out Sector neighbor) && neighbor != null)
                    neighbor.DrawGizmoArea(ActivationGizmos.Window);
        }

        // 겹침 구간에서 서 있는 섹터가 남의 이웃 색으로 덮이지 않게 나중에 그린다.
        foreach (Sector sector in _sectors)
            if (sector != null && sector.Contains(playerPos)) sector.DrawGizmoArea(ActivationGizmos.Standing);
    }
#endif
}
