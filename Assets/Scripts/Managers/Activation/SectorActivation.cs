using System.Collections.Generic;
using UnityEngine;

// ① 서 있는 섹터만 — 겹침 구간에선 서 있는 섹터가 둘이라 합집합으로 켠다.
public class SectorActivation : ActivationPolicy
{
    private Sector[] _sectors;

    public override void Init(ActivationLayout layout)
    {
        _sectors = layout._sectors;

        foreach (Sector sector in _sectors)
            sector.Init();
    }

    public override void SelectSpawners(Vector3 playerPos, HashSet<int> current, HashSet<int> result)
    {
        foreach (Sector sector in _sectors)
        {
            if (sector == null || !sector.Contains(playerPos)) continue;

            foreach (int id in sector.GetSpawnersID()) result.Add(id);
        }
    }

#if UNITY_EDITOR
    // 서 있는 섹터만 강조 (전체 박스는 Sector가 초록으로 그린다)
    public override void DrawGizmos(Vector3 playerPos)
    {
        foreach (Sector sector in _sectors)
            if (sector != null && sector.Contains(playerPos)) sector.DrawGizmoArea(ActivationGizmos.Standing);
    }
#endif
}
