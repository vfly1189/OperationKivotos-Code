using System.Collections.Generic;
using System.Threading;
using UnityEngine;


[System.Serializable]
public class MonsterData
{
    public int monsterId;
    public string addressableKey; // Addressables의 어드레스(키) 이름
    public string monsterName;
    public float hp;
    public float attack;
    public float defense;
}

[System.Serializable]
public class MonsterDataLoader : ILoader<int, MonsterData>
{
    public List<MonsterData> monsters = new List<MonsterData>();

    public Dictionary<int, MonsterData> MakeDict()
    {
        Dictionary<int, MonsterData> dict = new Dictionary<int, MonsterData>();
        foreach (MonsterData monster in monsters)
        {
            dict[monster.monsterId] = monster;
        }
        return dict;
    }
}
