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

public class ResourceManager
{
    // 리소스 캐싱(한번 로드한건 메모리에 들고 있자)
    // Key: 경로, Value: 리소스 원본
    Dictionary<string, Object> _resources = new Dictionary<string, Object>();

    private Dictionary<string, AsyncOperationHandle> _handles = new Dictionary<string, AsyncOperationHandle>();

    // [핵심] Addressables 핸들 관리용 딕셔너리 2개 분리
    // 1. 글로벌: 게임 종료 시까지 절대 해제되지 않음 (플레이어 캐릭터, UI, 공통 VFX 등)
    private Dictionary<string, AsyncOperationHandle> _globalHandles = new Dictionary<string, AsyncOperationHandle>();

    // 2. 씬: 씬 이동(Clear) 시마다 모두 해제되어 메모리 확보 (맵, 몬스터, 환경음 등)
    private Dictionary<string, AsyncOperationHandle> _sceneHandles = new Dictionary<string, AsyncOperationHandle>();

    public void Init()
    {
        // 필요하다면 초기화
        //_resources.Clear();
        //_handles.Clear();

        //global은 계속 살려둘거임
        _sceneHandles.Clear();
    }

    //// AssetReference를 받아 비동기로 로드하고 핸들을 관리하는 함수
    //public async Task<T> LoadAsync<T>(AssetReference assetRef, bool isGlobal = false) where T : UnityEngine.Object
    //{
    //    //if (assetRef == null || !assetRef.RuntimeKeyIsValid())
    //    //    return null;

    //    //string key = assetRef.RuntimeKey.ToString();

    //    //// 1. 이미 로드되었거나 로드 중인 핸들이 있다면
    //    //if (_handles.TryGetValue(key, out AsyncOperationHandle existingHandle))
    //    //{
    //    //    await existingHandle.Task; // 로딩이 안 끝났을 수 있으니 대기
    //    //    return existingHandle.Result as T;
    //    //}

    //    //// 2. 새로운 로드 요청
    //    //var handle = Addressables.LoadAssetAsync<T>(assetRef);
    //    //_handles.Add(key, handle); // 매니저가 핸들을 기억함

    //    //await handle.Task;

    //    //if (handle.Status == AsyncOperationStatus.Succeeded)
    //    //{
    //    //    return handle.Result;
    //    //}
    //    //else
    //    //{
    //    //    Debug.LogError($"[ResourceManager] Addressable Load Failed: {key}");
    //    //    _handles.Remove(key);
    //    //    return null;
    //    //}

    //    if (assetRef == null || !assetRef.RuntimeKeyIsValid())
    //        return null;

    //    string key = assetRef.RuntimeKey.ToString();

    //    // 1. 이미 캐시에 있는지 글로벌, 씬 양쪽 모두 확인
    //    if (_globalHandles.TryGetValue(key, out AsyncOperationHandle globalHandle))
    //    {
    //        await globalHandle.Task;
    //        return globalHandle.Result as T;
    //    }

    //    if (_sceneHandles.TryGetValue(key, out AsyncOperationHandle sceneHandle))
    //    {
    //        await sceneHandle.Task;
    //        return sceneHandle.Result as T;
    //    }

    //    // 2. 새로운 로드 요청
    //    var handle = Addressables.LoadAssetAsync<T>(assetRef);

    //    // 3. 플래그에 따라 다른 딕셔너리에 저장
    //    if (isGlobal)
    //        _globalHandles.Add(key, handle);
    //    else
    //        _sceneHandles.Add(key, handle);

    //    await handle.Task;

    //    if (handle.Status == AsyncOperationStatus.Succeeded)
    //    {
    //        return handle.Result as T;
    //    }
    //    else
    //    {
    //        Debug.LogError($"[ResourceManager] Addressable Load Failed: {key}");
    //        if (isGlobal) _globalHandles.Remove(key);
    //        else _sceneHandles.Remove(key);
    //        return null;
    //    }
    //}

    // [핵심 1] 반환형 Task<T> -> UniTask<T>
    public async UniTask<T> LoadAsync<T>(AssetReference assetRef, bool isGlobal = false) where T : UnityEngine.Object
    {
        if (assetRef == null || !assetRef.RuntimeKeyIsValid())
            return null;

        string key = assetRef.RuntimeKey.ToString();

        if (_globalHandles.TryGetValue(key, out AsyncOperationHandle globalHandle))
        {
            await globalHandle.ToUniTask(); // [핵심 2] .ToUniTask()로 어드레서블 핸들 연동
            return globalHandle.Result as T;
        }

        if (_sceneHandles.TryGetValue(key, out AsyncOperationHandle sceneHandle))
        {
            await sceneHandle.ToUniTask();
            return sceneHandle.Result as T;
        }

        var handle = Addressables.LoadAssetAsync<T>(assetRef);

        if (isGlobal) _globalHandles.Add(key, handle);
        else _sceneHandles.Add(key, handle);

        // Addressable 핸들을 UniTask 스케줄러에서 안전하게 대기
        await handle.ToUniTask();

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            return handle.Result as T;
        }
        else
        {
            Debug.LogError($"[ResourceManager] Addressable Load Failed: {key}");
            if (isGlobal) _globalHandles.Remove(key);
            else _sceneHandles.Remove(key);
            return null;
        }
    }

    //// Label 목록을 받아서 해당하는 모든 에셋을 메모리에 로드
    //// (StartScene에서는 isGlobal = true로, LoadingScene에서는 isGlobal = false로 넘김)
    //public async Task LoadDependenciesAsync(IEnumerable<string> labels, bool isGlobal = false, System.Action<string, float> onProgress = null)
    //{
    //    //// 1. 혹시 모를 로드 리스트 수집
    //    //var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);
    //    //await locationsHandle.Task;

    //    //if (locationsHandle.Status == AsyncOperationStatus.Succeeded)
    //    //{
    //    //    var locations = locationsHandle.Result;
    //    //    int totalCount = locations.Count;
    //    //    int currentCount = 0;

    //    //    // 2. 찾아낸 모든 에셋을 하나씩 로드하며 캐싱(_handles)
    //    //    foreach (var location in locations)
    //    //    {
    //    //        string key = location.PrimaryKey;


    //    //        // 이미 로드된 에셋인지 체크
    //    //        if (!_handles.ContainsKey(key))
    //    //        {
    //    //            var handle = Addressables.LoadAssetAsync<Object>(location);
    //    //            _handles.Add(key, handle);

    //    //            await handle.Task;
    //    //        }

    //    //        currentCount++;
    //    //        onProgress?.Invoke((float)currentCount / totalCount); // 로딩 UI 게이지 업데이트용
    //    //    }
    //    //}
    //    //else
    //    //{
    //    //    Debug.LogError("Failed to load resource locations by labels.");
    //    //}

    //    //Addressables.Release(locationsHandle);

    //    //var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);
    //    //await locationsHandle.Task;

    //    //if (locationsHandle.Status == AsyncOperationStatus.Succeeded)
    //    //{
    //    //    var locations = locationsHandle.Result;
    //    //    int totalCount = locations.Count;

    //    //    for (int i = 0; i < totalCount; i++)
    //    //    {
    //    //        var location = locations[i];
    //    //        string key = location.PrimaryKey; // 보통 파일 이름이나 주소가 들어있음
    //    //        Debug.Log($"Log : {key}");
    //    //        //if (!_handles.ContainsKey(key))
    //    //        //{
    //    //        //    var handle = Addressables.LoadAssetAsync<Object>(location);
    //    //        //    _handles.Add(key, handle);

    //    //        //    // Task.Yield()를 활용해 프레임마다 진행률을 체크 (Fake 없는 실제 로딩률)
    //    //        //    while (!handle.IsDone)
    //    //        //    {
    //    //        //        float currentAssetProgress = handle.PercentComplete;
    //    //        //        float overallProgress = (i + currentAssetProgress) / totalCount;

    //    //        //        onProgress?.Invoke(key, overallProgress);
    //    //        //        await Task.Yield(); // 다음 프레임까지 대기
    //    //        //    }
    //    //        //}

    //    //        // 이미 로드된 에셋인지 체크 (글로벌, 씬 양쪽 다)
    //    //        if (!_globalHandles.ContainsKey(key) && !_sceneHandles.ContainsKey(key))
    //    //        {
    //    //            var handle = Addressables.LoadAssetAsync<Object>(location);

    //    //            if (isGlobal) _globalHandles.Add(key, handle);
    //    //            else _sceneHandles.Add(key, handle);

    //    //            while (!handle.IsDone)
    //    //            {
    //    //                float currentAssetProgress = handle.PercentComplete;
    //    //                float overallProgress = (i + currentAssetProgress) / totalCount;
    //    //                onProgress?.Invoke(key, overallProgress);
    //    //                await Task.Yield();
    //    //            }
    //    //        }
    //    //        else
    //    //        {

    //    //        }

    //    //        // 해당 에셋 로드 완료 보장
    //    //        float completedProgress = (i + 1f) / totalCount;
    //    //        onProgress?.Invoke(key, completedProgress);
    //    //    }
    //    //}
    //    //else
    //    //{
    //    //    Debug.LogError("Failed to load resource locations by labels.");
    //    //}
    //    //// LoadResourceLocationsAsync 자체의 핸들은 사용 끝났으니 릴리즈 
    //    //Addressables.Release(locationsHandle);



    //    // 1. 라벨에 해당하는 모든 Location(경로) 가져오기
    //    var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);
    //    await locationsHandle.Task;

    //    if (locationsHandle.Status == AsyncOperationStatus.Succeeded)
    //    {
    //        var locations = locationsHandle.Result;
    //        int totalCount = locations.Count;
    //        int currentCount = 0; // 완료된 개수 추적용

    //        // Task들을 담을 리스트 생성
    //        List<Task> loadTasks = new List<Task>();

    //        foreach (var location in locations)
    //        {
    //            string key = location.PrimaryKey;
    //            Debug.Log($"[ResourceManager] 로드 요청 시작: {key}");

    //            // 캐시에 없는 파일만 로드 시작
    //            if (!_globalHandles.ContainsKey(key) && !_sceneHandles.ContainsKey(key))
    //            {
    //                var handle = Addressables.LoadAssetAsync<Object>(location);

    //                // 딕셔너리에 즉시 등록
    //                if (isGlobal) _globalHandles.Add(key, handle);
    //                else _sceneHandles.Add(key, handle);

    //                // Task.Yield 무한루프 대신, Event Action 방식이나 바로 Task를 대기시키는 방식으로 변경
    //                // 이렇게 해야 유니티 메인 스레드가 씬 전환 중에 멈추지 않습니다.

    //                // 개별 로드가 끝났을 때 실행될 로직을 Task로 묶어서 리스트에 추가
    //                var task = handle.Task.ContinueWith(t =>
    //                {
    //                    currentCount++;
    //                    float progress = (float)currentCount / totalCount;
    //                    // 메인 스레드에서 UI 업데이트를 위해 람다 내부에서 호출 시 주의 필요하지만,
    //                    // 유니티 2022+ 에서는 기본적으로 동기화 컨텍스트가 유지됩니다.
    //                    onProgress?.Invoke(key, progress);
    //                }, TaskScheduler.FromCurrentSynchronizationContext());

    //                loadTasks.Add(task);
    //            }
    //            else
    //            {
    //                // 이미 캐시에 있는 거면 즉시 카운트 증가
    //                currentCount++;
    //                onProgress?.Invoke(key, (float)currentCount / totalCount);
    //            }
    //        }

    //        // [핵심 해결 지점]
    //        // while문으로 프레임을 강제로 쪼개며 대기하지 않고, 
    //        // C# 표준인 Task.WhenAll을 사용하여 모든 비동기 로딩이 끝날 때까지 한 번에 대기합니다.
    //        if (loadTasks.Count > 0)
    //        {
    //            await Task.WhenAll(loadTasks);
    //        }

    //        Debug.Log($"[ResourceManager] 모든 Dependencies 로드 완료!");
    //    }
    //    else
    //    {
    //        Debug.LogError("[ResourceManager] Failed to load resource locations by labels.");
    //    }

    //    // 위치 검색 핸들은 해제
    //    Addressables.Release(locationsHandle);
    //}

    //// async Task를 제거하고 코루틴을 반환하도록 수정
    //public IEnumerator LoadDependenciesCoroutine(IEnumerable<string> labels, bool isGlobal = false, System.Action<string, float> onProgress = null)
    //{
    //    // 1. 라벨로 위치 찾기
    //    var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);

    //    // Task.await 대신 유니티 엔진에 맞게 yield return으로 대기
    //    yield return locationsHandle;

    //    if (locationsHandle.Status == AsyncOperationStatus.Succeeded)
    //    {
    //        var locations = locationsHandle.Result;
    //        int totalCount = locations.Count;

    //        for (int i = 0; i < totalCount; i++)
    //        {
    //            var location = locations[i];
    //            string key = location.PrimaryKey;

    //            if (!_globalHandles.ContainsKey(key) && !_sceneHandles.ContainsKey(key))
    //            {
    //                var handle = Addressables.LoadAssetAsync<Object>(location);

    //                if (isGlobal) _globalHandles.Add(key, handle);
    //                else _sceneHandles.Add(key, handle);

    //                // 코루틴 내에서 프레임 단위로 안전하게 대기
    //                while (!handle.IsDone)
    //                {
    //                    float currentAssetProgress = handle.PercentComplete;
    //                    float overallProgress = (i + currentAssetProgress) / totalCount;
    //                    onProgress?.Invoke(key, overallProgress);

    //                    yield return null; // 유니티 메인 스레드의 다음 프레임까지 대기 (데드락 없음)
    //                }
    //            }

    //            // 하나의 파일 로드 완료
    //            float completedProgress = (i + 1f) / totalCount;
    //            onProgress?.Invoke(key, completedProgress);
    //        }
    //    }
    //    else
    //    {
    //        Debug.LogError("[ResourceManager] 라벨 로드 실패!");
    //    }

    //    Addressables.Release(locationsHandle);
    //}

    // [핵심 3] Task -> UniTask 로 변경
    public async UniTask LoadDependenciesAsync(IEnumerable<string> labels, bool isGlobal = false, System.Action<string, float> onProgress = null)
    {
        var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);
        await locationsHandle.ToUniTask();

        if (locationsHandle.Status == AsyncOperationStatus.Succeeded)
        {
            var locations = locationsHandle.Result;
            int totalCount = locations.Count;

            for (int i = 0; i < totalCount; i++)
            {
                var location = locations[i];
                string key = location.PrimaryKey;
                Debug.Log($"[ResourceManager] 로드 요청 시작: {key}");

                if (!_globalHandles.ContainsKey(key) && !_sceneHandles.ContainsKey(key))
                {
                    var handle = Addressables.LoadAssetAsync<Object>(location);

                    if (isGlobal) _globalHandles.Add(key, handle);
                    else _sceneHandles.Add(key, handle);

                    // [핵심 4] while문을 돌며 대기할 때, 유니티 프레임 단위(Update)로 안전하게 양보
                    // Task.Yield()가 일으키던 데드락(멈춤)을 완벽히 해결!
                    while (!handle.IsDone)
                    {
                        float currentAssetProgress = handle.PercentComplete;
                        float overallProgress = (i + currentAssetProgress) / totalCount;
                        onProgress?.Invoke(key, overallProgress);

                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }
                }

                float completedProgress = (i + 1f) / totalCount;
                onProgress?.Invoke(key, completedProgress);
            }
        }
        else
        {
            Debug.LogError("Failed to load resource locations by labels.");
        }

        Addressables.Release(locationsHandle);
    }

    // [추가] 이미 로드된(캐싱된) 에셋을 즉시 가져오는 동기 함수
    public T GetLoadedAsset<T>(AssetReference assetRef) where T : UnityEngine.Object
    {
        if (assetRef == null || !assetRef.RuntimeKeyIsValid())
            return null;

        // Addressables는 이미 로드된 객체에 대해 LoadAssetAsync를 부르면, 
        // 디스크 I/O 없이 메모리에 있는 걸 즉시 반환(Succeeded 상태)합니다.
        var handle = Addressables.LoadAssetAsync<T>(assetRef);

        // [핵심 해결책] 
        // 메모리에 이미 있다면 WaitForCompletion()은 렉 없이 즉시 완료됩니다.
        // 만약 메모리에 없다면 여기서 동기 로딩이 걸리면서 에셋을 찾아옵니다.
        T result = handle.WaitForCompletion();

        if (result != null)
        {
            return result;
        }
        else
        {
            Debug.LogError($"[ResourceManager] 에셋 동기 로드 실패! : {assetRef.RuntimeKey}");
            Addressables.Release(handle);
            return null;
        }
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
        //// 1. 이미 비동기 로딩을 통해 캐시(_handles)에 올라와 있는지 확인
        //if (_handles.TryGetValue(key, out AsyncOperationHandle handle))
        //{
        //    if (handle.Status == AsyncOperationStatus.Succeeded)
        //    {
        //        GameObject original = handle.Result as GameObject;
        //        if (original != null)
        //        {
        //            // 2. 찾았으면 기존에 잘 짜두신 안전한 Instantiate 함수(Warp 처리 포함)를 호출!
        //            return Instantiate(original, position, rotation, parent);
        //        }
        //    }
        //}

        //// 캐시에 없다면 에러 로그 (스포너가 호출하기 전에 해당 씬에서 프리로딩이 안 되었다는 뜻)
        //Debug.LogError($"[ResourceManager] 에셋이 로드되지 않았거나 찾을 수 없습니다. Key: {key}\n" +
        //               $"미리 LoadAsync로 로딩해두었는지 확인하세요.");
        //return null;

        // 1. 캐시에서 찾기 (글로벌 우선, 그 다음 씬)
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
        //foreach(var handle in _handles.Values)
        //{
        //    if (handle.IsValid())
        //    {
        //        Addressables.Release(handle);
        //    }
        //}
        //_handles.Clear();

        // _globalHandles는 건드리지 않고, _sceneHandles만 Release하여 메모리 확보
        foreach (var handle in _sceneHandles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle); // Addressable 레퍼런스 카운트 감소 (메모리 해제) [web:50]
            }
        }
        _sceneHandles.Clear();

        // 동기 로드용 _resources 딕셔너리도 비워줍니다 (Resources 폴더 사용을 대비)
        _resources.Clear();
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
