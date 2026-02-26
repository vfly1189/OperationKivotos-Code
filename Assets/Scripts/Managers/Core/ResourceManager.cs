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

public class ResourceManager
{
    // 리소스 캐싱(한번 로드한건 메모리에 들고 있자)
    // Key: 경로, Value: 리소스 원본
    Dictionary<string, Object> _resources = new Dictionary<string, Object>();

    private Dictionary<string, AsyncOperationHandle> _handles = new Dictionary<string, AsyncOperationHandle>();

    public void Init()
    {
        // 필요하다면 초기화
        //_resources.Clear();
        _handles.Clear();
    }

    // AssetReference를 받아 비동기로 로드하고 핸들을 관리하는 함수
    public async Task<T> LoadAsync<T>(AssetReference assetRef) where T : UnityEngine.Object
    {
        if (assetRef == null || !assetRef.RuntimeKeyIsValid())
            return null;

        string key = assetRef.RuntimeKey.ToString();

        // 1. 이미 로드되었거나 로드 중인 핸들이 있다면
        if (_handles.TryGetValue(key, out AsyncOperationHandle existingHandle))
        {
            await existingHandle.Task; // 로딩이 안 끝났을 수 있으니 대기
            return existingHandle.Result as T;
        }

        // 2. 새로운 로드 요청
        var handle = Addressables.LoadAssetAsync<T>(assetRef);
        _handles.Add(key, handle); // 매니저가 핸들을 기억함

        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            return handle.Result;
        }
        else
        {
            Debug.LogError($"[ResourceManager] Addressable Load Failed: {key}");
            _handles.Remove(key);
            return null;
        }
    }

    // Label 목록을 받아서 해당하는 모든 에셋을 메모리에 로드(캐싱)하는 함수
    public async Task LoadDependenciesAsync(IEnumerable<string> labels, System.Action<float> onProgress = null)
    {
        // 1. 혹시 모를 로드 리스트 수집
        var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);
        await locationsHandle.Task;

        if (locationsHandle.Status == AsyncOperationStatus.Succeeded)
        {
            var locations = locationsHandle.Result;
            int totalCount = locations.Count;
            int currentCount = 0;

            // 2. 찾아낸 모든 에셋을 하나씩 로드하며 캐싱(_handles)
            foreach (var location in locations)
            {
                string key = location.PrimaryKey;


                // 이미 로드된 에셋인지 체크
                if (!_handles.ContainsKey(key))
                {
                    var handle = Addressables.LoadAssetAsync<Object>(location);
                    _handles.Add(key, handle);

                    await handle.Task;
                }

                currentCount++;
                onProgress?.Invoke((float)currentCount / totalCount); // 로딩 UI 게이지 업데이트용
            }
        }
        else
        {
            Debug.LogError("Failed to load resource locations by labels.");
        }

        Addressables.Release(locationsHandle);
    }

    // [기존] 동기 로드 (급할 때 사용)
    public T Load<T>(string path) where T : Object
    {
        // 1. 캐시에 있는지 확인
        if (_resources.TryGetValue(path, out Object resource))
        {
            return resource as T;
        }

        // 2. 없으면 리소스 폴더에서 로드
        T loadResult = Resources.Load<T>(path);

        // 3. 찾았으면 캐시에 저장
        if (loadResult != null)
        {
            _resources.Add(path, loadResult);
        }
        else
        {
            Debug.LogError($"Failed to Load Resource: {path}");
        }

        return loadResult;
    }


    public GameObject Instantiate(string path, Transform parent = null)
    {
        // 동기 로드를 사용 (이미 로딩씬에서 LoadAsync로 캐시에 올려두었다면 즉시 리턴됨)
        GameObject original = Load<GameObject>($"Prefabs/{path}");

        if (original == null)
        {
            Debug.Log($"Failed to load prefab : {path}");
            return null;
        }

        GameObject go = Object.Instantiate(original, parent);
        go.name = original.name; // (Clone) 떼기
        return go;
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

    // [추가] Addressable Key 문자열을 받아 위치/회전까지 맞춰주는 Instantiate 함수
    public GameObject Instantiate(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        // 1. 이미 비동기 로딩을 통해 캐시(_handles)에 올라와 있는지 확인
        if (_handles.TryGetValue(key, out AsyncOperationHandle handle))
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject original = handle.Result as GameObject;
                if (original != null)
                {
                    // 2. 찾았으면 기존에 잘 짜두신 안전한 Instantiate 함수(Warp 처리 포함)를 호출!
                    return Instantiate(original, position, rotation, parent);
                }
            }
        }

        // 캐시에 없다면 에러 로그 (스포너가 호출하기 전에 해당 씬에서 프리로딩이 안 되었다는 뜻)
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
        foreach(var handle in _handles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
        _handles.Clear();
        // Addressable을 쓰면 UnloadUnusedAssets나 GC.Collect를 강제 호출할 필요가 없습니다.
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
