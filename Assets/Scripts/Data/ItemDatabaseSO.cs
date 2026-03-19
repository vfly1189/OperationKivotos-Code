using System;
using System.Collections.Generic;
using UnityEngine;

#region StatPool
public enum EStatType
{
    MaxHP_Flat,
    Attack_Flat,
    Defense_Flat,
    MaxHP_Percent,
    Attack_Percent,
    Defense_Percent,
    MoveSpeed,
    CritRate,
    CritDamage,
    EnergyRegen
}

public enum UpgradeBookID
{
    Small = 30003,
    Medium,
    Large
}

[System.Serializable]
public class StatPoolEntry
{
    public EStatType StatType;  // 어떤 스탯인가?
    public int Weight;          // 뽑힐 확률 가중치
    public float BaseValue;     // 기본 수치
    //public float UpgradeValue;  // 강화당 증가 수치

    public float UpgradeMinValue;
    public float UpgradeMaxValue;
}

[System.Serializable]
public class StatPoolData
{
    public int PoolID;          // 예: 1101, 2101
    public string Note;         // 에디터 확인용 메모 

    // 이 PoolID에 속하는 모든 스탯 옵션들의 리스트
    public List<StatPoolEntry> Entries = new List<StatPoolEntry>();
}
#endregion

[System.Serializable]
public class GradeConfig
{
    public ItemGrade Grade;
    public int MaxLevel;
    public int InitialSubStatCount;
}


[System.Serializable]
public class BaseItemData
{
    public int ID;
    public string Name;
    public string IconKey;
    public ItemGrade Grade;
    public string Description;
}

// 1. 장비 전용 클래스 (Equipment 시트와 일치)
[System.Serializable]
public class EquipmentData : BaseItemData
{
    public string EquipPart;
    public int Tier;


    public int MainStatPoolID;
    public int SubStatPoolID;
}

// 2. 소모품 전용 클래스 (Consumable 시트와 일치)
[System.Serializable]
public class ConsumableData : BaseItemData
{
    public float EffectValue;
    public float Duration;
    public int MaxStack;
}

// 3. 재료 전용 클래스 (Material 시트와 일치)
[System.Serializable]
public class MaterialData : BaseItemData
{
    public int Tier;
    public int MaxStack;
}

// [엑셀 EquipmentDecomposition 시트용]
[System.Serializable]
public class EquipmentDecompositionData
{
    public int Tier;
    public int Mat1_ID;     // 소형 재료 ID (30003)
    public int Mat1_Count;
    public int Mat2_ID;     // 중형 재료 ID (30004)
    public int Mat2_Count;
    public int Mat3_ID;     // 대형 재료 ID (30005)
    public int Mat3_Count;
}

// [엑셀 EquipmentUpgradeBookExp 시트용] - 따로 만드는 게 맞습니다!
[System.Serializable]
public class EquipmentUpgradeBookExpData
{
    public int Mat_ID;  // 재료 ID (30003, 30004, 30005)
    public int ExpValue;    // 이 재료 1개가 주는 EXP (200, 600, 1800)
}

// [엑셀 EquipmentUpgradeBookExp 시트용] - 따로 만드는 게 맞습니다!
[System.Serializable]
public class EquipmentLevelExpData
{
    public int Level;  
    public int RequireExp;
}

[System.Serializable]
public class EquipmentUpgradeCost
{
    public int Level;
    public int EnhancementCost;
}

// 데이터베이스 통합 SO
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Data/ItemDatabase")]
public class ItemDatabaseSO : ScriptableObject, IDataCacheable
{
    public List<EquipmentData> Equipments = new List<EquipmentData>();
    public List<ConsumableData> Consumables = new List<ConsumableData>();
    public List<MaterialData> Materials = new List<MaterialData>();
    public List<StatPoolData> StatPools = new List<StatPoolData>();
    public List<EquipmentDecompositionData> DecompositionData = new List<EquipmentDecompositionData>();
    public List<EquipmentUpgradeBookExpData> UpgradeBookExpData = new List<EquipmentUpgradeBookExpData>();
    public List<EquipmentLevelExpData> EquipmentLevelExpData = new List<EquipmentLevelExpData>();
    public List<EquipmentUpgradeCost> EquipmentUpgradeCost = new List<EquipmentUpgradeCost>();
    public List<GradeConfig> GradeConfigData = new List<GradeConfig>();

    // List를 Dictionary로 변환해서 넘겨주는 헬퍼 함수들
    public Dictionary<int, EquipmentData> MakeEquipDict()
    {
        Dictionary<int, EquipmentData> dict = new Dictionary<int, EquipmentData>();
        foreach (var item in Equipments) dict[item.ID] = item;
        return dict;
    }

    public Dictionary<int, ConsumableData> MakeConsumableDict()
    {
        Dictionary<int, ConsumableData> dict = new Dictionary<int, ConsumableData>();
        foreach (var item in Consumables) dict[item.ID] = item;
        return dict;
    }

    public Dictionary<int, MaterialData> MakeMaterialDict()
    {
        Dictionary<int, MaterialData> dict = new Dictionary<int, MaterialData>();
        foreach (var item in Materials) dict[item.ID] = item;
        return dict;
    }

    public Dictionary<int, StatPoolData> MakeStatPoolDict()
    {
        Dictionary<int, StatPoolData> dict = new Dictionary<int, StatPoolData>();
        foreach (var pool in StatPools) dict[pool.PoolID] = pool;
        return dict;
    }

    public Dictionary<int, EquipmentDecompositionData> MakeDecompDict()
    {
        Dictionary<int, EquipmentDecompositionData> dict = new Dictionary<int, EquipmentDecompositionData>();
        foreach (var item in DecompositionData) dict[item.Tier] = item;
        return dict;
    }
    public Dictionary<int, EquipmentUpgradeBookExpData> MakeBookExpDict() 
    {
        Dictionary<int, EquipmentUpgradeBookExpData> dict = new Dictionary<int, EquipmentUpgradeBookExpData>();
        foreach (var item in UpgradeBookExpData) dict[item.Mat_ID] = item;
        return dict;
    }

    public Dictionary<int, EquipmentLevelExpData> MakeEquipmentLevelExpDict()
    {
        Dictionary<int, EquipmentLevelExpData> dict = new Dictionary<int, EquipmentLevelExpData>();
        foreach (var item in EquipmentLevelExpData) dict[item.Level] = item;
        return dict;
    }

    public Dictionary<int, EquipmentUpgradeCost> MakeEquipmentUpgradeCostDict()
    {
        Dictionary<int, EquipmentUpgradeCost> dict = new Dictionary<int, EquipmentUpgradeCost>();
        foreach (var item in EquipmentUpgradeCost) dict[item.Level] = item;
        return dict;
    }

    public Dictionary<ItemGrade, GradeConfig> MakeGradeConfigDict()
    {
        Dictionary<ItemGrade, GradeConfig> dict = new Dictionary<ItemGrade, GradeConfig>();
        foreach (var item in GradeConfigData) dict[item.Grade] = item;
        return dict;
    }


    // IDataCacheable 구현부: DataManager가 이 함수를 호출해줌
    public void CacheData(Dictionary<Type, object> dataDicts)
    {
        dataDicts[typeof(EquipmentData)] = MakeEquipDict();
        dataDicts[typeof(ConsumableData)] = MakeConsumableDict();
        dataDicts[typeof(MaterialData)] = MakeMaterialDict();
        dataDicts[typeof(StatPoolData)] = MakeStatPoolDict();
        dataDicts[typeof(EquipmentDecompositionData)] = MakeDecompDict();
        dataDicts[typeof(EquipmentUpgradeBookExpData)] = MakeBookExpDict();
        dataDicts[typeof(EquipmentLevelExpData)] = MakeEquipmentLevelExpDict();
        dataDicts[typeof(EquipmentUpgradeCost)] = MakeEquipmentUpgradeCostDict();
        dataDicts[typeof(GradeConfig)] = MakeGradeConfigDict();
    }
}