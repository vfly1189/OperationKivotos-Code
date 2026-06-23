using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponEnhanceCost
{
    public int TargetLevel;      // 강화 후 도달할 레벨 (2~25)
    public int RequireGold;      // 필요 골드

    public int Material1ID;      // 재료 1번 아이템 ID
    public int Material1Count;   // 재료 1번 개수

    public int Material2ID;      // 재료 2번 아이템 ID (0이면 불필요)
    public int Material2Count;   // 재료 2번 개수

    public int Material3ID;      // 재료 3번 아이템 ID (0이면 불필요)
    public int Material3Count;   // 재료 3번 개수
}

[CreateAssetMenu(fileName = "WeaponEnhanceCostTable", menuName = "Data/WeaponEnhanceCostTable")]
public class WeaponEnhanceCostTableSO : ScriptableObject, IDataCacheable
{
    [Header("Weapon Enhancement Cost Data (Level 2~25)")]
    // 엑셀의 Weapon_Enhance_Cost_Table을 파싱해서 채워질 리스트
    public List<WeaponEnhanceCost> CostList = new List<WeaponEnhanceCost>();

    public void CacheData(Dictionary<Type, object> dataDicts)
    {
        dataDicts[typeof(WeaponEnhanceCost)] = MakeDictionary();
    }

    // ==========================================
    // 특정 목표 레벨의 강화 비용 가져오기
    // ==========================================
    public WeaponEnhanceCost GetCostByTargetLevel(int targetLevel)
    {
        // 리스트를 순회하며 찾기
        foreach (var cost in CostList)
        {
            if (cost.TargetLevel == targetLevel)
            {
                return cost;
            }
        }

        GameLog.LogError($"[WeaponEnhanceCostTable] {targetLevel} 레벨의 강화 비용 데이터가 없습니다.");
        return default;
    }

    // ==========================================
    // (선택) Dictionary로 변환하는 헬퍼 함수 (DataManager용)
    // ==========================================
    public Dictionary<int, WeaponEnhanceCost> MakeDictionary()
    {
        Dictionary<int, WeaponEnhanceCost> dict = new Dictionary<int, WeaponEnhanceCost>();
        foreach (var cost in CostList)
        {
            dict[cost.TargetLevel] = cost;
        }
        return dict;
    }
}