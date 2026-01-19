using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

using Object = UnityEngine.Object;

public class ResourceManager
{
    // 리소스 캐싱(한번 로드한건 메모리에 들고 있자)
    // Key: 경로, Value: 리소스 원본
    Dictionary<string, Object> _resources = new Dictionary<string, Object>();

    public void Init()
    {
        // 필요하다면 초기화
        _resources.Clear();
    }

    // [기존] 동기 로드 (급할 때 사용)
    public T Load<T>(string path) where T : Object
    {
        // 1. 캐시에 있는지 확인
        if (_resources.TryGetValue(path, out Object resource))
            return resource as T;

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

    // [추가] 비동기 로드 (로딩씬에서 사용)
    // path: 경로, callback: 로딩 끝났을 때 실행할 함수
    public void LoadAsync<T>(string path, Action<T> callback = null) where T : Object
    {
        // 1. 이미 캐시에 있다면 바로 콜백 실행
        if (_resources.TryGetValue(path, out Object resource))
        {
            callback?.Invoke(resource as T);
            return;
        }

        // 2. 코루틴을 돌려야 하므로 Managers(MonoBehaviour)에게 위임해야 함
        Managers.Start_Coroutine(CoLoadAsync(path, callback));
    }

    // 실제 비동기 로딩 코루틴
    System.Collections.IEnumerator CoLoadAsync<T>(string path, Action<T> callback) where T : Object
    {
        ResourceRequest request = Resources.LoadAsync<T>(path);

        // 로딩이 끝날때까지 대기
        while (!request.isDone)
        {
            yield return null;
        }

        if (request.asset != null)
        {
            // 캐시에 없다면 추가 (중복 체크)
            if (!_resources.ContainsKey(path))
                _resources.Add(path, request.asset);

            callback?.Invoke(request.asset as T);
        }
        else
        {
            Debug.LogError($"Failed to Load Async: {path}");
            callback?.Invoke(null);
        }
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

        // 풀링 체크 로직 (생략 가능)
        // if (original.GetComponent<Poolable>() != null) ...

        GameObject go = Object.Instantiate(original, parent);
        go.name = original.name; // (Clone) 떼기
        return go;
    }

    // 메모리 정리 (씬 이동 시 호출)
    public void Clear()
    {
        // 언로드 가능한 에셋들은 다 날림
        _resources.Clear();
        Resources.UnloadUnusedAssets();
    }
    public void Destroy(GameObject go)
    {
        if (go == null)
            return;

        ////만약에 풀링이 필요한 아이라면 -> 풀링 매니저한테 위탁
        //Poolable poolable = go.GetComponent<Poolable>();
        //if (poolable != null)
        //{
        //    Managers.Pool.Push(poolable);
        //    return;
        //}

        Object.Destroy(go);
    }
}
