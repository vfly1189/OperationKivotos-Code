using System;
using System.Collections.Generic;
using UnityEngine;

public class MonsterDefine
{
    public enum MonsterSpawnType
    {
        None,
        Field,
        Dungeon
    }

    public enum MonsterGrade
    {
        None,
        Normal,
        Elite,
        Boss
    }
}

[Serializable]
public class MonsterBaseData
{
    public int ID;
    public string MonsterName;
    public string AddressableKey;
    public MonsterDefine.MonsterSpawnType SpawnType;
    public MonsterDefine.MonsterGrade Grade;
    public float BaseMaxHP;
    public float BaseAttack;
    public float BaseDefense;
    public float BaseSpeed;
    public int DropTableID;
    public int ExpReward;
    public int CreditReward;
}

[Serializable]
public class MonsterLevelByStat
{
    public int Level;
    public float MaxHPRate;
    public float AttackRate;
    public float DefenseRate;
    public float CreditRate;
}

[Serializable]
public class MapMonsterConfig
{
    public int MapID;
    public string MapName;
    public int NormalMonsterLevel;
    public int EliteMonsterLevel;
}

// AddressableKey 딕셔너리를 구분하기 위한 빈 마커 클래스 - MonsterBaseData
public class MonsterAddressableMarker { }

[CreateAssetMenu(fileName = "MonsterDatabase", menuName = "Data/MonsterDatabase")]
public class MonsterDatabaseSO : ScriptableObject, IDataCacheable
{
    public List<MonsterBaseData> MonsterBaseDatas = new List<MonsterBaseData>();
    public List<MonsterLevelByStat> MonsterLevelByStats = new List<MonsterLevelByStat>();
    public List<MapMonsterConfig> MonsterConfigs = new List<MapMonsterConfig>();

    public Dictionary<int, MonsterBaseData> MakeMonsterBaseDataDict()
    {
        Dictionary<int, MonsterBaseData> dict = new Dictionary<int, MonsterBaseData>();
        foreach (var item in MonsterBaseDatas) dict[item.ID] = item;
        return dict;
    }

    // [추가] AddressableKey(string)를 Key로 하는 딕셔너리 생성
    public Dictionary<string, MonsterBaseData> MakeMonsterAddressableDict()
    {
        Dictionary<string, MonsterBaseData> dict = new Dictionary<string, MonsterBaseData>();
        foreach (var item in MonsterBaseDatas)
        {
            // 비어있지 않은 경우에만 추가
            if (!string.IsNullOrEmpty(item.AddressableKey))
            {
                dict[item.AddressableKey] = item;
            }
        }
        return dict;
    }

    public Dictionary<int, MonsterLevelByStat> MakeMonsterLevelByStatDict()
    {
        Dictionary<int, MonsterLevelByStat> dict = new Dictionary<int, MonsterLevelByStat>();
        foreach (var item in MonsterLevelByStats) dict[item.Level] = item;
        return dict;
    }

    public Dictionary<int, MapMonsterConfig> MakeMonsterConfigDict()
    {
        Dictionary<int, MapMonsterConfig> dict = new Dictionary<int, MapMonsterConfig>();
        foreach (var item in MonsterConfigs) dict[item.MapID] = item;
        return dict;
    }

    public void CacheData(Dictionary<Type, object> dataDicts)
    {
        dataDicts[typeof(MonsterBaseData)] = MakeMonsterBaseDataDict();
        // 2. [추가] AddressableKey(string) 기준 딕셔너리 캐싱 (Marker 사용)
        dataDicts[typeof(MonsterAddressableMarker)] = MakeMonsterAddressableDict();


        dataDicts[typeof(MonsterLevelByStat)] = MakeMonsterLevelByStatDict();
        dataDicts[typeof(MapMonsterConfig)] = MakeMonsterConfigDict();        
    }
}