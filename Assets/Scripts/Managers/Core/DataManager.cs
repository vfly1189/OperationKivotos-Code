using System.Collections.Generic;
using UnityEngine;

// [추가] UniTask
using Cysharp.Threading.Tasks;
using System;

public class DataManager
{
    //public Dictionary<string, WeaponData> WeaponDict { get; private set; } = new Dictionary<string, WeaponData>();
    //public Dictionary<int, EnhancementRateData> EnhanceRateDict { get; private set; } = new Dictionary<int, EnhancementRateData>();
    //public Dictionary<int, SpawnerData> SpawnerDict { get; private set; } = new Dictionary<int, SpawnerData>();
    //public Dictionary<int, MonsterData> MonsterDict { get; private set; } = new Dictionary<int, MonsterData>();

    // [핵심 변경] 모든 종류의 딕셔너리를 담는 "만능 바구니"
    // Key: 데이터 클래스 타입 (예: typeof(MonsterData))
    // Value: 해당 데이터를 담은 딕셔너리 객체 (object로 업캐스팅하여 저장)
    private Dictionary<Type, object> _dataDicts = new Dictionary<Type, object>();


    // [핵심 1] 코루틴 대신 UniTask로 선언
    public async UniTask InitAsync()
    {
        // Addressable에서 읽어올 작업 리스트
        var tasks = new List<UniTask>();

        // [리팩토링] 데이터 추가 시 여기에 한 줄만 쓰면 끝!
        //tasks.Add(LoadAndCacheJsonAsync<WeaponDataLoader, string, WeaponData>("Character_Weapon_Data"));
        tasks.Add(LoadAndCacheJsonAsync<WeaponEnhanceMentDataLoader, int, EnhancementRateData>("Weapon_Enhancement_Rate_Data"));
        tasks.Add(LoadAndCacheJsonAsync<SpawnerDataLoader, int, SpawnerData>("SpawnerData"));
        tasks.Add(LoadAndCacheJsonAsync<MonsterDataLoader, int, MonsterData>("MonsterData"));
        // 아이템 SO를 로드하고 싶다면?
        tasks.Add(LoadAndCacheSOAsync<ItemDatabaseSO>("ItemDatabase"));
        tasks.Add(LoadAndCacheSOAsync<CharacterExpTableSO>("CharacterExpTable"));

        tasks.Add(LoadAndCacheSOAsync<WeaponEnhanceCostTableSO>("WeaponEnhanceCostTable"));

        // 병렬로 한 방에 다운로드 및 파싱
        await UniTask.WhenAll(tasks);

        Debug.Log($"DataManager Init Complete: 총 {_dataDicts.Count}개의 데이터 테이블 로드 완료.");


        //// Task들을 정의만 해둡니다.
        //var weaponTask = LoadJsonAsync<WeaponDataLoader, string, WeaponData>("Character_Weapon_Data");
        //var enhanceTask = LoadJsonAsync<WeaponEnhanceMentDataLoader, int, EnhancementRateData>("Weapon_Enhancement_Rate_Data");
        //var spawnerTask = LoadJsonAsync<SpawnerDataLoader, int, SpawnerData>("SpawnerData");
        //var monsterTask = LoadJsonAsync<MonsterDataLoader, int, MonsterData>("MonsterData");

        //// [핵심 수정] WhenAll이 반환하는 결과를 Tuple로 한 방에 받아냅니다!
        //// 이렇게 하면 내부적으로 Task를 두 번 참조하지 않게 되어 에러가 발생하지 않습니다.
        //var (weaponResult, enhanceResult, spawnerResult, monsterResult) =
        //    await UniTask.WhenAll(weaponTask, enhanceTask, spawnerTask, monsterTask);

        //// 받아온 결과물(딕셔너리)을 할당합니다.
        //WeaponDict = weaponResult;
        //EnhanceRateDict = enhanceResult;
        //SpawnerDict = spawnerResult;
        //MonsterDict = monsterResult;

        //Debug.Log("DataManager Init Complete");
    }

    //// [핵심 3] 콜백을 없애고 딕셔너리를 직접 반환
    //// [핵심 변경] Addressables 직접 호출 제거, ResourceManager로 위임
    //private async UniTask<Dictionary<Key, Value>> LoadJsonAsync<Loader, Key, Value>(string key)
    //    where Loader : ILoader<Key, Value>
    //{
    //    // ResourceManager의 NoCache 함수를 사용하여 텍스트 에셋을 가져옴 (메모리 해제는 매니저가 알아서 함)
    //    TextAsset textAsset = await Managers.Resource.LoadAsyncNoCache<TextAsset>(key);

    //    if (textAsset != null)
    //    {
    //        Loader loader = JsonUtility.FromJson<Loader>(textAsset.text);
    //        return loader.MakeDict();
    //    }
    //    else
    //    {
    //        Debug.LogError($"Failed to load Addressable JSON: {key}");
    //        return new Dictionary<Key, Value>();
    //    }
    //}

    // =========================================================
    // SO 파일을 Addressables로 로드하고 딕셔너리로 분해하여 바구니에 담는 함수
    // =========================================================
    private async UniTask LoadAndCacheSOAsync<T>(string addressableKey) where T : ScriptableObject, IDataCacheable
    {
        T soData = await Managers.Resource.LoadAsync<T>(addressableKey);

        if (soData != null)
        {
            // 인터페이스 함수를 호출하여 각 SO가 알아서 바구니에 담도록 지시
            soData.CacheData(_dataDicts);
        }
        else
        {
            Debug.LogError($"[DataManager] SO 로드 실패: {addressableKey} (Type: {typeof(T).Name})");
        }
    }

    // (참고용) 기존 JSON 로더 (이름만 좀 명확하게 바꿈)
    private async UniTask LoadAndCacheJsonAsync<Loader, TKey, TValue>(string addressableKey)
        where Loader : ILoader<TKey, TValue>
    {
        TextAsset textAsset = await Managers.Resource.LoadAsyncNoCache<TextAsset>(addressableKey);
        if (textAsset != null)
        {
            Loader loader = JsonUtility.FromJson<Loader>(textAsset.text);
            _dataDicts.Add(typeof(TValue), loader.MakeDict());
        }
    }

    // ==========================================================
    // [만능 Getter]
    //데이터 종류가 늘어나도 GetWeaponData(), GetMonsterData()를 계속 만들 필요가 없습니다!
    // ==========================================================
    public TValue GetData<TKey, TValue>(TKey key) where TValue : class
    {
        // 1. 내가 찾는 데이터 타입(TValue)의 딕셔너리가 바구니에 있는지 확인
        if (_dataDicts.TryGetValue(typeof(TValue), out object dictObj))
        {
            // 2. 있다면 원래 딕셔너리 형태로 캐스팅 (object -> Dictionary<TKey, TValue>)
            var dict = dictObj as Dictionary<TKey, TValue>;

            // 3. 딕셔너리 안에서 해당하는 Key값을 찾음
            if (dict != null && dict.TryGetValue(key, out TValue data))
            {
                return data;
            }
        }

        Debug.LogError($"[DataManager] {typeof(TValue).Name} 데이터에서 키 [{key}]를 찾을 수 없습니다!");
        return null; // 못 찾으면 null
    }

    public BaseItemData GetItemData(int itemID, ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Equipment:
                return GetData<int, EquipmentData>(itemID);
            case ItemCategory.Consumable:
                return GetData<int, ConsumableData>(itemID);
            case ItemCategory.Material:
                return GetData<int, MaterialData>(itemID);
            default:
                return null;
        }
    }
}
