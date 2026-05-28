using System;
using System.Collections.Generic;
using UnityEngine;


public enum DungeonType
{
    None = 0,
    Normal,
    Boss,
}


[Serializable]
public class DungeonData
{
    public int GroupID;
    public int DungeonID;               // 실제 맵 로드에 쓰일 ID
    public Define.DungeonDifficulty Difficulty;

    //기획 추가 대비용
    public int EnterCost;               // 입장 재화(행동력)
    public int RequiredLevel;           // 권장 레벨

    public int ClearExp;
    public int ClearCredit;
    public int ClearDropTableID;
}

[Serializable]
public class DungeonGroup
{
    public int GroupID;
    public string DungeonName;          // 다국어 적용시 int NameKey 로 변경 권장
    public DungeonType Type;

    // 1. 에디터 저장 및 인스펙터 노출용 리스트 (직렬화 됨)
    public List<DungeonData> DungeonDataList = new List<DungeonData>();

    // 2. 런타임 읽기용 딕셔너리 (직렬화 안됨)
    [NonSerialized]
    public Dictionary<Define.DungeonDifficulty, DungeonData> DungeonDataByDifficulty 
        = new Dictionary<Define.DungeonDifficulty, DungeonData>();

    public DungeonData GetDungeonData(Define.DungeonDifficulty difficulty)
    {
        return DungeonDataByDifficulty[difficulty];
    }

}



[CreateAssetMenu(fileName = "DungeonDatabaseSO", menuName = "Data/DungeonDatabase")]
public class DungeonDatabaseSO : ScriptableObject, IDataCacheable
{
    public List<DungeonGroup> DungeonGroups = new List<DungeonGroup>();

    public Dictionary<int, DungeonGroup> MakeDungeonGroupDict()
    {
        Dictionary<int, DungeonGroup> dict = new Dictionary<int, DungeonGroup>();

        foreach (var group in DungeonGroups)
        {
            // 런타임 조회를 위해 List의 데이터를 Dictionary로 변환 (캐싱)
            group.DungeonDataByDifficulty = new Dictionary<Define.DungeonDifficulty, DungeonData>();
            foreach (var data in group.DungeonDataList)
            {
                group.DungeonDataByDifficulty[data.Difficulty] = data;
            }

            dict[group.GroupID] = group;
        }
        return dict;
    }

    public void CacheData(Dictionary<Type, object> dataDicts)
    {
        dataDicts[typeof(DungeonGroup)] = MakeDungeonGroupDict();
    }
}
