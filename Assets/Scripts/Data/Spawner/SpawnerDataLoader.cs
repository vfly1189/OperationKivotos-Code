using System.Collections.Generic;
using UnityEngine;

// 1. JSON의 "spawnList" 배열 안에 들어갈 개별 스폰 정보 클래스
[System.Serializable]
public class SpawnInfo
{
    public int pointIndex;
    public int monsterId;
    public float delay;
}

[System.Serializable] // <<<<< 이거 필수!
public class SpawnerData
{
    public int spawnerId;
    public List<SpawnInfo> spawnList = new List<SpawnInfo>();
}

[System.Serializable]
public class SpawnerDataLoader : ILoader<int, SpawnerData>
{
    public List<SpawnerData> spawners = new List<SpawnerData>();

    public Dictionary<int, SpawnerData> MakeDict()
    {
        Dictionary<int, SpawnerData> dict = new Dictionary<int, SpawnerData>();
        foreach (SpawnerData spanwer in spawners)
        {
            dict[spanwer.spawnerId] = spanwer;
        }
        return dict;
    }
}