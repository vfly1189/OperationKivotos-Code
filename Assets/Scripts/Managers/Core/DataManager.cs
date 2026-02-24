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

    // [추가] 강화 확률 딕셔너리
    public Dictionary<int, EnhancementRateData> EnhanceRateDict { get; private set; } = new Dictionary<int, EnhancementRateData>();

    //public void Init()
    //{
    //    // JSON 로드 및 딕셔너리 변환
    //    WeaponDict = LoadJson<WeaponDataLoader, string, WeaponData>("Character_Weapon_Data").MakeDict();
    //    EnhanceRateDict = LoadJson<WeaponEnhanceMentDataLoader, int, EnhancementRateData>("Weapon_Enhancement_Rate_Data").MakeDict();
    //}

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

        Debug.Log("DataManager Init Complete");
    }

    //// 제네릭 JSON 로더
    //// Loader: ILoader를 구현한 래퍼 클래스 (예: WeaponDataLoader)
    //// Key, Value: 딕셔너리 키/값 타입
    //private Loader LoadJson<Loader, Key, Value>(string path) where Loader : ILoader<Key, Value>
    //{
    //    TextAsset textAsset = Managers.Resource.Load<TextAsset>($"Data/{path}"); // Resources/Data/ 경로 사용

    //    if (textAsset == null)
    //    {
    //        Debug.LogError($"Failed to load JSON data: Data/{path}");
    //        return default(Loader);
    //    }
    //    // JsonUtility로 파싱
    //    return JsonUtility.FromJson<Loader>(textAsset.text);
    //}

    //private Loader LoadJson<Loader, Key, Value>(string key) where Loader : ILoader<Key, Value>
    //{
    //    // 1. Addressable로 TextAsset 로드 (동기 대기)
    //    var handle = Addressables.LoadAssetAsync<TextAsset>(key);

    //    // 로드 완료될 때까지 메인 스레드 멈추고 대기 (주의: 프레임 드랍 발생 가능)
    //    TextAsset textAsset = handle.WaitForCompletion();

    //    if (handle.Status == AsyncOperationStatus.Succeeded && textAsset != null)
    //    {
    //        // 2. 파싱
    //        Loader loader = JsonUtility.FromJson<Loader>(textAsset.text);

    //        // 3. 메모리 해제 (TextAsset은 텍스트만 따오면 필요 없음)
    //        Addressables.Release(handle);

    //        return loader;
    //    }
    //    else
    //    {
    //        Debug.LogError($"Failed to load Addressable JSON: {key}");
    //        return default(Loader);
    //    }
    //}

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
}
