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
        // 딕셔너리에 없거나 죽은(null) 섹터면 무시 (안전 장치)
        if (!_sectors.ContainsKey(sectorId) || _sectors[sectorId] == null) return;

        Debug.Log($"{_sectors[sectorId].gameObject.name}에 진입");

        if (_currentActiveSector != null && _currentActiveSector.SectorId == sectorId)
            return;

        // Unity 객체는 null 체크 시 == null 외에 내부 생존 여부도 확인해야 할 수 있음.
        // 연산자는 C# 레벨의 널 체크이므로, Unity 파괴 객체를 체크하기 위해 명시적으로 검사
        if (_currentActiveSector != null && _currentActiveSector.gameObject != null)
        {
            _currentActiveSector.DeactivateSector();
        }

        _currentActiveSector = _sectors[sectorId];
        _currentActiveSector?.ActivateSector();
    }

    public void Clear()
    {
        _sectors.Clear();
        _currentActiveSector = null; //씬 전환 시 현재 섹터도 반드시 비워줘야 합니다.
    }
}
