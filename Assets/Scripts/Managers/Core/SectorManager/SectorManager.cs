using System.Collections.Generic;
using UnityEngine;

public class SectorManager
{
    private Dictionary<int, Sector> _sectors = new Dictionary<int, Sector>();
    private Sector _currentActiveSector;

    public void RegisterSector(Sector sector)
    {
        // 이미 같은 ID의 섹터가 등록되어 있다면 덮어쓰거나 경고 
        if (_sectors.ContainsKey(sector._sectorID))
        {
            GameLog.LogWarning($"[SectorManager] 중복된 SectorId({sector._sectorID})가 등록되었습니다.");
        }

        _sectors[sector._sectorID] = sector;
    }

    // 플레이어가 특정 구역에 진입했을 때 호출
    public void OnPlayerEnterSector(int sectorId)
    {
        // 딕셔너리에 없거나 죽은 섹터면 무시 
        if (!_sectors.ContainsKey(sectorId) || _sectors[sectorId] == null) return;

        GameLog.Log($"{_sectors[sectorId].gameObject.name}에 진입");

        if (_currentActiveSector != null && _currentActiveSector._sectorID == sectorId)
            return;

        if (_currentActiveSector != null && _currentActiveSector.gameObject != null)
        {
            _currentActiveSector.DeactivateSector();
        }

        _currentActiveSector = _sectors[sectorId];
        _currentActiveSector?.ActivateSector();
    }

    // 모든 섹터를 동시에 활성화
    public void ActivateAllSectors()
    {
        foreach (var kvp in _sectors)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
                kvp.Value.ActivateSector();
        }

        // 전체 활성화 상태에서는 단일 추적 대상이 없으므로 초기화
        _currentActiveSector = null;
    }

    // 모든 섹터를 동시에 비활성화
    public void DeactivateAllSectors()
    {
        foreach (var kvp in _sectors)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
                kvp.Value.DeactivateSector();
        }

        _currentActiveSector = null;
    }

    public void Clear()
    {
        _sectors.Clear();
        _currentActiveSector = null; //씬 전환 시 현재 섹터도 반드시 비워줘야 합니다.
    }
}
