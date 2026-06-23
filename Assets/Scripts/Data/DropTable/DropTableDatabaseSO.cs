using System;
using System.Collections.Generic;
using UnityEngine;

public class DropTableDefine
{
    public enum RewardType
    {
        None,
        Equipment,
        Material
    }
}


[Serializable]
public class DropTableEntry
{
    public int DropTableID;
    public DropTableDefine.RewardType RewardType;
    public int ItemID;
    public int Tier;
    public int Weight;
    public int MaxCount;
    public int MinCount;
}

//[Serializable]
//public class DungeonTable
//{
//    public int DungeonID;
//    public string DungeonName;
//    public int ClearExp;
//    public int ClearCredit;
//    public int ClearDropTableID;
//}


[Serializable]
public class DropTable
{
    public int DropTableID;
    public string DropTableName;
    public int Rolls;

    public List<DropTableEntry> Entries = new List<DropTableEntry>();

    // 런타임 확률 굴림을 위한 가중치 합 (직렬화 안 해도 됨)
    [NonSerialized] public int TotalWeight;
}



[CreateAssetMenu(fileName = "DropTableDatabase", menuName = "Data/DropTableDatabase")]
public class DropTableDatabaseSO : ScriptableObject, IDataCacheable
{
    public List<DropTable> DropTables = new List<DropTable>();
    //public List<DungeonTable> DungeonTables = new List<DungeonTable>();

    public Dictionary<int, DropTable> MakeDropTableDict()
    {
        Dictionary<int, DropTable> dict = new Dictionary<int, DropTable>();
        foreach (var item in DropTables) dict[item.DropTableID] = item;
        return dict;
    }

    //public Dictionary<int, DungeonTable> MakeDungeonTableDict()
    //{
    //    Dictionary<int, DungeonTable> dict = new Dictionary<int, DungeonTable>();
    //    foreach (var item in DungeonTables) dict[item.DungeonID] = item;
    //    return dict;
    //}

    public void CacheData(Dictionary<Type, object> dataDicts)
    {

        foreach (var item in DropTables)
        {
            // 캐싱할 때 TotalWeight도 같이 계산해줍니다.
            item.TotalWeight = 0;
            foreach (var entry in item.Entries)
            {
                item.TotalWeight += entry.Weight;
            }
        }

        dataDicts[typeof(DropTable)] = MakeDropTableDict();
        //dataDicts[typeof(DungeonTable)] = MakeDungeonTableDict();
    }
}
