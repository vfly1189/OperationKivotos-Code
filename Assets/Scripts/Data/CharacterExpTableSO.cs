using System;
using System.Collections.Generic;
using UnityEngine;

// 한 레벨에 해당하는 데이터 (엑셀의 한 줄)
[Serializable]
public class LevelExpData
{
    public int Level;
    public int RequireExp;
}


[CreateAssetMenu(fileName = "CharacterExpTable", menuName = "Data/CharacterExpTable")]
public class CharacterExpTableSO : ScriptableObject, IDataCacheable
{
    // 엑셀에서 추출한 1~30레벨 경험치가 쭉 담길 리스트
    public List<LevelExpData> ExpList = new List<LevelExpData>();

    // 런타임에서 특정 레벨의 필요 경험치를 쉽게 빼오기 위한 함수
    public int GetRequireExp(int level)
    {
        // 레벨 1이 인덱스 0이므로 (level - 1)
        int index = level - 1;
        if (index >= 0 && index < ExpList.Count)
        {
            return ExpList[index].RequireExp;
        }

        Debug.LogError($"[ExpTable] {level} 레벨의 경험치 데이터가 없습니다. 최대 레벨일 수 있습니다.");
        return 0;
    }
    
    public void CacheData(Dictionary<Type, object> dataDicts)
    {
        // 리스트를 그대로 담아도 되고, Dictionary로 변환해도 됩니다.
        // 레벨이 고유값이므로 딕셔너리가 편합니다.
        Dictionary<int, LevelExpData> dict = new Dictionary<int, LevelExpData>();
        foreach (var data in ExpList)
        {
            dict[data.Level] = data;
        }

        dataDicts[typeof(LevelExpData)] = dict;
    }
}
