using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class DataManager
{
    // 데이터 저장소
    public Dictionary<string, WeaponData> WeaponDict { get; private set; } = new Dictionary<string, WeaponData>();
    // 추가 예시: public Dictionary<int, MonsterData> MonsterDict { get; private set; }

    // 강화 확률 딕셔너리
    public Dictionary<int, EnhancementRateData> EnhanceRateDict { get; private set; } = new Dictionary<int, EnhancementRateData>();

    public Dictionary<int, SpawnerData> SpawnerDict { get; private set; } = new Dictionary<int, SpawnerData>();

    public Dictionary<int, MonsterData> MonsterDict { get; private set; } = new Dictionary<int, MonsterData>();


    public IEnumerator InitCoroutine()
    {
        yield return LoadJsonCoroutine<WeaponDataLoader, string, WeaponData>(
            "Character_Weapon_Data",
            loader => WeaponDict = loader.MakeDict()
        );

        yield return LoadJsonCoroutine<WeaponEnhanceMentDataLoader, int, EnhancementRateData>(
            "Weapon_Enhancement_Rate_Data",
            loader => EnhanceRateDict = loader.MakeDict()
        );

        yield return LoadJsonCoroutine<SpawnerDataLoader, int, SpawnerData>(
            "SpawnerData",
            loader => SpawnerDict = loader.MakeDict()
        );

        yield return LoadJsonCoroutine<MonsterDataLoader, int, MonsterData>(
            "MonsterData",
            loader => MonsterDict = loader.MakeDict()
        );

        Debug.Log("DataManager Init Complete");
    }


    private IEnumerator LoadJsonCoroutine<Loader, Key, Value>(string key, System.Action<Loader> onLoaded)
    where Loader : ILoader<Key, Value>
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>(key);
        yield return handle; // 메인 스레드 안 막음, 다음 프레임에서 완료 처리

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            Loader loader = JsonUtility.FromJson<Loader>(handle.Result.text);
            Addressables.Release(handle);
            onLoaded?.Invoke(loader);
        }
        else
        {
            Debug.LogError($"Failed to load Addressable JSON: {key}");
            Addressables.Release(handle);
        }
    }




    // ==========================================================
    // [핵심 유틸리티 함수]
    // 외부(스포너 등)에서 ID를 주면, 매칭되는 Addressable Key를 바로 뱉어줍니다.
    // ==========================================================
    public MonsterData GetMonsterDataById(int monsterId)
    {
        if (MonsterDict.TryGetValue(monsterId, out MonsterData data))
        {
            return data;
        }

        Debug.LogError($"ID [{monsterId}]에 해당하는 몬스터 데이터가 없습니다!");
        return null;
    }

    public SpawnerData GetSpawnerData(int spawnerId)
    {
        if (SpawnerDict.TryGetValue(spawnerId, out SpawnerData data))
        {
            return data;
        }

        Debug.LogError($"ID [{spawnerId}]에 해당하는 스포너 데이터가 없습니다!");
        return null;
    }
}
