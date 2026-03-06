using System;
using System.Collections.Generic;
using UnityEngine;

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
    //public int ID;
    //public string IconKey;
    //public string Grade;
    //public string Name;
    public string EquipPart;
    public int Tier;
    public float MaxHP;
    public float Attack;
    public float Defense;
    public float MoveSpeed;
    //public string Description;
}

// 2. 소모품 전용 클래스 (Consumable 시트와 일치)
[System.Serializable]
public class ConsumableData : BaseItemData
{
    //public int ID;
    //public string IconKey;
    //public string Grade;
    //public string Name;
    public float EffectValue;
    public float Duration;
    public int MaxStack;
    //public string Description;
}

// 3. 재료 전용 클래스 (Material 시트와 일치)
[System.Serializable]
public class MaterialData : BaseItemData
{
    //public int ID;
    //public string IconKey;
    //public string Grade;
    //public string Name;
    public int Tier;
    public int MaxStack;
    //public string Description;
}

// 데이터베이스 통합 SO
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Data/ItemDatabase")]
public class ItemDatabaseSO : ScriptableObject, IDataCacheable
{
    public List<EquipmentData> Equipments = new List<EquipmentData>();
    public List<ConsumableData> Consumables = new List<ConsumableData>();
    public List<MaterialData> Materials = new List<MaterialData>();

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

    // IDataCacheable 구현부: DataManager가 이 함수를 호출해줌
    public void CacheData(Dictionary<Type, object> dataDicts)
    {
        dataDicts[typeof(EquipmentData)] = MakeEquipDict();
        dataDicts[typeof(ConsumableData)] = MakeConsumableDict();
        dataDicts[typeof(MaterialData)] = MakeMaterialDict();
    }
}