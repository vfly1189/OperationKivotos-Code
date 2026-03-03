using System.Collections.Generic;
using UnityEngine;

// [추가] UniTask
using Cysharp.Threading.Tasks;

public class DataManager
{
    public Dictionary<string, WeaponData> WeaponDict { get; private set; } = new Dictionary<string, WeaponData>();
    public Dictionary<int, EnhancementRateData> EnhanceRateDict { get; private set; } = new Dictionary<int, EnhancementRateData>();
    public Dictionary<int, SpawnerData> SpawnerDict { get; private set; } = new Dictionary<int, SpawnerData>();
    public Dictionary<int, MonsterData> MonsterDict { get; private set; } = new Dictionary<int, MonsterData>();

    // [핵심 1] 코루틴 대신 UniTask로 선언
    public async UniTask InitAsync()
    {
        // Task들을 정의만 해둡니다.
        var weaponTask = LoadJsonAsync<WeaponDataLoader, string, WeaponData>("Character_Weapon_Data");
        var enhanceTask = LoadJsonAsync<WeaponEnhanceMentDataLoader, int, EnhancementRateData>("Weapon_Enhancement_Rate_Data");
        var spawnerTask = LoadJsonAsync<SpawnerDataLoader, int, SpawnerData>("SpawnerData");
        var monsterTask = LoadJsonAsync<MonsterDataLoader, int, MonsterData>("MonsterData");

        // [핵심 수정] WhenAll이 반환하는 결과를 Tuple로 한 방에 받아냅니다!
        // 이렇게 하면 내부적으로 Task를 두 번 참조하지 않게 되어 에러가 발생하지 않습니다.
        var (weaponResult, enhanceResult, spawnerResult, monsterResult) =
            await UniTask.WhenAll(weaponTask, enhanceTask, spawnerTask, monsterTask);

        // 받아온 결과물(딕셔너리)을 할당합니다.
        WeaponDict = weaponResult;
        EnhanceRateDict = enhanceResult;
        SpawnerDict = spawnerResult;
        MonsterDict = monsterResult;

        Debug.Log("DataManager Init Complete");
    }

    // [핵심 3] 콜백을 없애고 딕셔너리를 직접 반환
    // [핵심 변경] Addressables 직접 호출 제거, ResourceManager로 위임
    private async UniTask<Dictionary<Key, Value>> LoadJsonAsync<Loader, Key, Value>(string key)
        where Loader : ILoader<Key, Value>
    {
        // ResourceManager의 NoCache 함수를 사용하여 텍스트 에셋을 가져옴 (메모리 해제는 매니저가 알아서 함)
        TextAsset textAsset = await Managers.Resource.LoadAsyncNoCache<TextAsset>(key);

        if (textAsset != null)
        {
            Loader loader = JsonUtility.FromJson<Loader>(textAsset.text);
            return loader.MakeDict();
        }
        else
        {
            Debug.LogError($"Failed to load Addressable JSON: {key}");
            return new Dictionary<Key, Value>();
        }
    }

    // ==========================================================
    public MonsterData GetMonsterDataById(int monsterId)
    {
        if (MonsterDict.TryGetValue(monsterId, out MonsterData data)) return data;
        Debug.LogError($"ID [{monsterId}]에 해당하는 몬스터 데이터가 없습니다!");
        return null;
    }

    public SpawnerData GetSpawnerData(int spawnerId)
    {
        if (SpawnerDict.TryGetValue(spawnerId, out SpawnerData data)) return data;
        Debug.LogError($"ID [{spawnerId}]에 해당하는 스포너 데이터가 없습니다!");
        return null;
    }
}
