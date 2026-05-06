using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;
using Object = UnityEngine.Object;

using Cysharp.Threading.Tasks;
using UnityEngine.U2D;

public class ResourceManager
{
    // Addressables 핸들 관리용 딕셔너리 2개 분리
    // 1. 글로벌: 게임 종료 시까지 절대 해제되지 않음 (플레이어 캐릭터, UI, 공통 VFX 등)
    private Dictionary<string, AsyncOperationHandle> _globalHandles = new Dictionary<string, AsyncOperationHandle>();

    // 2. 씬: 씬 이동(Clear) 시마다 모두 해제되어 메모리 확보 (맵, 몬스터, 환경음 등)
    private Dictionary<string, AsyncOperationHandle> _sceneHandles = new Dictionary<string, AsyncOperationHandle>();

    //아틀라스 파편(Sprite) 보호용 강력한 글로벌 캐시
    private Dictionary<string, Sprite> _atlasSpriteCache = new Dictionary<string, Sprite>();



    public void Init()
    {
        //global은 계속 살려둘거임
        _sceneHandles.Clear();

        
    }

    // =========================================================================
    // 1. AssetReference를 인자로 받는 LoadAsync (씬에서 주로 사용)
    // =========================================================================
    public async UniTask<T> LoadAsync<T>(AssetReference assetRef, bool isGlobal = false) where T : UnityEngine.Object
    {
        if (assetRef == null || !assetRef.RuntimeKeyIsValid())
            return null;

        // AssetReference의 런타임 키를 string으로 변환해서 내부 처리 함수로 넘김
        return await LoadAsync<T>(assetRef.RuntimeKey.ToString(), isGlobal);
    }
  
    public UniTask<T> LoadAsync<T>(string key, bool isGlobal = false) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key)) return UniTask.FromResult<T>(null);

        if (_globalHandles.TryGetValue(key, out var gh) && gh.IsDone && gh.Result is T gResult)
            return UniTask.FromResult(gResult);

        if (_sceneHandles.TryGetValue(key, out var sh) && sh.IsDone && sh.Result is T sResult)
            return UniTask.FromResult(sResult);

        return LoadAsyncInternal<T>(key, isGlobal);
    }

    // 실제 비동기 로직 분리
    private async UniTask<T> LoadAsyncInternal<T>(string key, bool isGlobal) where T : UnityEngine.Object
    {

        bool wasGlobal = false;
        AsyncOperationHandle handleToRelease = default;

        // 1. 타입 불일치 핸들 제거 및 원래 글로벌 소속이었는지 기억하기
        if (_globalHandles.TryGetValue(key, out var gh) && gh.IsDone && !(gh.Result is T))
        {
            wasGlobal = true; // 아! 얘는 처음에 Global로 프리로드 했던 애구나!
            _globalHandles.Remove(key);
            handleToRelease = gh; // 즉시 해제하면 메모리가 날아갈 수 있으니 임시 보관
        }
        if (_sceneHandles.TryGetValue(key, out var sh) && sh.IsDone && !(sh.Result is T))
        {
            _sceneHandles.Remove(key);
            handleToRelease = sh;
        }

        // 로딩 중인 핸들 있으면 기다리기
        if (_globalHandles.TryGetValue(key, out var pendingGlobal) && !pendingGlobal.IsDone)
        {
            await pendingGlobal.ToUniTask();
            return pendingGlobal.Result as T;
        }
        if (_sceneHandles.TryGetValue(key, out var pendingScene) && !pendingScene.IsDone)
        {
            await pendingScene.ToUniTask();
            return pendingScene.Result as T;
        }

        // 2. 새로운 타입(예: Sprite)으로 다시 로드
        var handle = Addressables.LoadAssetAsync<T>(key);


        if (isGlobal || wasGlobal) _globalHandles[key] = handle;
        else _sceneHandles[key] = handle;

        await handle.ToUniTask();

        // 새 핸들 로드가 완전히 끝난 후 예전 핸들 해제 
        if (handleToRelease.IsValid())
        {
            Addressables.Release(handleToRelease);
        }

        if (handle.Status == AsyncOperationStatus.Succeeded) return handle.Result as T;

        Debug.LogError($"[ResourceManager] Load Failed: {key}");
        if (isGlobal || wasGlobal) _globalHandles.Remove(key);
        else _sceneHandles.Remove(key);
        return null;
    }


    // =========================================================================
    // 3. NoCache 로드 (문자열 string Key 기반) -> DataManager에서 JSON 부를 때 사용
    // =========================================================================
    public async UniTask<T> LoadAsyncNoCache<T>(string key) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key)) return null;

        // string key를 사용해 로드
        var handle = Addressables.LoadAssetAsync<T>(key);

        // ToUniTask로 대기할 때 에러가 나면 잡을 수 있도록 안전하게 처리
        T result = null;
        try
        {
            result = await handle.ToUniTask();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ResourceManager] NoCache Load Exception: {key} / {e.Message}");
        }

        if (handle.Status == AsyncOperationStatus.Succeeded && result != null)
        {
            Addressables.Release(handle);
            return result;
        }
        else
        {
            Debug.LogError($"[ResourceManager] Addressable NoCache Load Failed: {key}");
            // 실패했을 때도 핸들이 유효하면 메모리 해제
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }
    }

    // =========================================================================
    //  SpriteAtlas 특화 로드 및 추출 함수
    // =========================================================================
    public async UniTask<Sprite> GetSpriteFromAtlasAsync(string atlasKey, string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;

        // 1. 방어 캐시에 안전하게 보관 중이라면 반환
        if (_atlasSpriteCache.TryGetValue(spriteName, out Sprite cachedSprite))
        {
            if (cachedSprite != null) return cachedSprite;
        }

        // 2. 캐시에 없으면 아틀라스 자체를 어드레서블로 로드
        SpriteAtlas atlas = await LoadAsync<SpriteAtlas>(atlasKey, isGlobal: true);

        if (atlas != null)
        {
            // 3. 아틀라스를 여는 순간, 내부의 모든 Sprite 조각을 캐시에 등록
            Sprite[] allSprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(allSprites);

            foreach (var s in allSprites)
            {
                // (Clone) 글자 떼기
                string cleanName = s.name.Replace("(Clone)", "");

                // 캐시에 등록
                if (!_atlasSpriteCache.ContainsKey(cleanName))
                {
                    _atlasSpriteCache.Add(cleanName, s);
                }
            }

            // 4. 이제 안전하게 캐시에서 꺼내서 반환
            if (_atlasSpriteCache.TryGetValue(spriteName, out Sprite targetSprite))
            {
                return targetSprite;
            }
            else
            {
                Debug.LogWarning($"[ResourceManager] '{atlasKey}' 아틀라스에 '{spriteName}' 이미지가 없습니다.");
            }
        }
        return null;
    }


    // =========================================================================
    // 프리로딩 전용 함수
    // =========================================================================
    public async UniTask LoadDependenciesAsync(IEnumerable<string> labels, bool isGlobal = false, System.Action<string, float> onProgress = null)
    {    
        var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);
        await locationsHandle.ToUniTask();

        if (locationsHandle.Status != AsyncOperationStatus.Succeeded) return;

        var locations = locationsHandle.Result;
        int totalCount = locations.Count;

        for (int i = 0; i < totalCount; i++)
        {
            var location = locations[i];
            string key = location.PrimaryKey;

            Debug.Log($"로딩 키 : {key}");

            // 이미 딕셔너리에 있으면 스킵 (중복 로드 방지)
            if (_globalHandles.ContainsKey(key) || _sceneHandles.ContainsKey(key))
            {
                onProgress?.Invoke(key, (i + 1f) / totalCount);
                continue;
            }

            //  Object 타입으로 로드하되 딕셔너리에 저장
            var handle = Addressables.LoadAssetAsync<Object>(location);

            if (isGlobal) _globalHandles[key] = handle;
            else _sceneHandles[key] = handle;

            while (!handle.IsDone)
            {
                onProgress?.Invoke(key, (i + handle.PercentComplete) / totalCount);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            onProgress?.Invoke(key, (i + 1f) / totalCount);
        }

        Addressables.Release(locationsHandle);
    }

    public GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        // 1. 원본 프리팹의 활성화 상태를 잠시 끄고 복사
        // (이렇게 하면 생성될 때 Awake는 돌지만 OnEnable과 물리 처리는 돌지 않음)
        bool wasActive = original.activeSelf;
        if (wasActive) original.SetActive(false);
        // 1. 생성 (풀링 혹은 인스턴스화)
        GameObject go = Instantiate(original, parent); // 기존 Instantiate(GameObject) 활용


        // 원상 복구
        if (wasActive) original.SetActive(true);


        // go가 제대로 생성되었을 때만 처리 (안전망)
        if (go != null)
        {
            // 2. 위치/회전 설정
            var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();

            if (agent != null)
            {
                // [Agent가 있는 경우] 
                // 위치는 무조건 Warp로 이동시켜야 씹히지 않음
                agent.Warp(position);
                // 단, Warp는 회전을 처리해주지 않으므로 회전은 따로 적용
                go.transform.rotation = rotation;
            }
            else
            {
                // [Agent가 없는 경우 (플레이어 등)] 
                // 일반적인 Transform 방식으로 위치와 회전 모두 적용
                go.transform.position = position;
                go.transform.rotation = rotation;
            }

            // 3. 모든 세팅이 완벽히 끝난 후 오브젝트 활성화
            go.SetActive(true);
        }

        return go;
    }

    //  Addressable Key 문자열을 받아 위치/회전까지 맞춰주는 Instantiate 함수
    public GameObject Instantiate(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        // 캐시에서 찾기 (글로벌 우선, 그 다음 씬)
        AsyncOperationHandle handle;
        bool found = _globalHandles.TryGetValue(key, out handle) || _sceneHandles.TryGetValue(key, out handle);

        if (found && handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject original = handle.Result as GameObject;
            if (original != null)
            {
                // 찾았으면 기존 안전한 Instantiate(GameObject) 활용
                return Instantiate(original, position, rotation, parent);
            }
        }

        Debug.LogError($"[ResourceManager] 에셋이 로드되지 않았거나 찾을 수 없습니다. Key: {key}\n" +
                       $"미리 LoadAsync로 로딩해두었는지 확인하세요.");
        return null;
    }


    public GameObject Instantiate(GameObject original, Transform parent = null)
    {
        // 1. Poolable이 붙어있으면 풀 매니저에게 위임
        if (original.GetComponent<Poolable>() != null)
        {
            return Managers.Pool.Pop(original, parent).gameObject;
        }

        // 2. 아니면 그냥 생성
        GameObject go = Object.Instantiate(original, parent);
        go.name = original.name; // (Clone) 떼기
        return go;
    }



    // 메모리 정리 (씬 이동 시 호출)
    public void Clear()
    {
        // _globalHandles는 건드리지 않고, _sceneHandles만 Release하여 메모리 확보
        foreach (var handle in _sceneHandles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle); // Addressable 레퍼런스 카운트 감소 (메모리 해제)
            }
        }
        _sceneHandles.Clear();
    }


    public void Destroy(GameObject go)
    {
        if (go == null)
            return;

        //만약에 풀링이 필요한 아이라면 -> 풀링 매니저한테 위탁
        Poolable poolable = go.GetComponent<Poolable>();
        if (poolable != null)
        {
            Managers.Pool.Push(poolable);
            return;
        }

        Object.Destroy(go);
    }
}
