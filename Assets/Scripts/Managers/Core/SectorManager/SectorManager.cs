using System.Collections.Generic;
using UnityEngine;

public class SectorManager
{
    private Dictionary<int, Sector> _sectors = new Dictionary<int, Sector>();
    private Sector _currentActiveSector;

    public void RegisterSector(Sector sector)
    {
        _sectors[sector.SectorId] = sector;
    }

    // 플레이어가 특정 구역에 진입했을 때 호출
    public void OnPlayerEnterSector(int sectorId)
    {
        Debug.Log($"{_sectors[sectorId].gameObject.name}에 진입");
        // 이미 같은 구역이면 무시
        if (_currentActiveSector != null && _currentActiveSector.SectorId == sectorId)
            return;

        // 이전 구역 비활성화 (거리가 멀어졌을 때 몬스터를 되돌리는 로직)
        _currentActiveSector?.DeactivateSector();

        //// 새 구역 활성화 (몬스터 스폰)
        //if (_sectors.TryGetValue(sectorId, out Sector newSector))
        //{
        //    _currentActiveSector = newSector;
        //    _currentActiveSector.ActivateSector();
        //}
    }
}
