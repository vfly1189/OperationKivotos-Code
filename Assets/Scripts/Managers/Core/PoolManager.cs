using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AI;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PoolManager
{
    class Pool
    {
        public GameObject Original { get; private set; }
        public Transform Root;

        Stack<Poolable> _poolStack = new Stack<Poolable>();
        private AsyncOperationHandle<GameObject> _handle; //핸들 보관

        public void Init(GameObject original, AsyncOperationHandle<GameObject> handle, int count = 5)
        {
            Original = original;
            _handle = handle; // 핸들 저장
            Root = new GameObject().transform;
            Root.name = $"{original.name}_Pool_Root";

            for (int i = 0; i < count; i++)
                Push(Create());
        }

        Poolable Create()
        {
            GameObject go = Object.Instantiate(Original);

            // 만약 활성화 상태로 생성되었다면, 위치를 잡기 전에 Agent를 꺼버립니다.
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            go.name = Original.name;
            go.transform.position = Vector3.zero;

            return go.GetOrAddComponent<Poolable>();
        }

        public void Push(Poolable poolable)
        {
            if (poolable == null) return;

            poolable.transform.SetParent(Root);
            poolable.gameObject.SetActive(false);
            poolable.IsUsing = false;

            _poolStack.Push(poolable);
        }

        public Poolable Pop(Transform parent)
        {
            Poolable poolable;

            if (_poolStack.Count > 0)
                poolable = _poolStack.Pop();
            else
                poolable = Create();

            // 위치 초기화 부분 수정
            poolable.transform.SetParent(parent ?? Managers.SceneEx.CurrentScene.transform);
            poolable.transform.localScale = Vector3.one;

            // NavMeshAgent가 붙어있다면, 위치를 옮기기 전에 반드시 꺼야 합니다.
            NavMeshAgent agent = poolable.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            //// Transform 초기화 (안전장치)
            //// 1. 스케일 리셋 (가장 중요)
            //poolable.transform.localScale = Vector3.one;

            //// 2. 위치/회전 리셋 (필요하다면)
            ////poolable.transform.localPosition = Vector3.zero;
            ////poolable.transform.localRotation = Quaternion.identity;

            //poolable.IsUsing = true;

            //// 부모 설정
            //if (parent == null)
            //    poolable.transform.SetParent(Managers.SceneEx.CurrentScene.transform);
            //else
            //    poolable.transform.SetParent(parent);

            // 활성화는 ResourceManager에서 위치 잡은 뒤에 함
            // poolable.gameObject.SetActive(true); 
            return poolable;
        }


        // 풀 정리 시 핸들 Release
        public void Release()
        {
            if (_handle.IsValid())
                Addressables.Release(_handle);
        }
    }

    Dictionary<string, Pool> _pool = new Dictionary<string, Pool>();
    Transform _root;
    public void Init()
    {
        if (_root == null)
        {
            _root = new GameObject { name = "@Pool_Root" }.transform;
            Object.DontDestroyOnLoad(_root);
        }



    }

    // 풀 생성 (미리 만들어두기)
    public void CreatePool(GameObject original, AsyncOperationHandle<GameObject> handle, int count = 10)
    {
        //이미 풀이 존재하면 중복 생성하지 않고 리턴
        if (_pool.ContainsKey(original.name))
            return;

        Pool pool = new Pool();
        pool.Init(original, handle, count);
        pool.Root.SetParent(_root);

        _pool.Add(original.name, pool);
    }

    public void CreatePool(GameObject original, int count = 10)
    {
        if (_pool.ContainsKey(original.name))
            return;

        Pool pool = new Pool();
        // 빈 핸들로 초기화 (일반 프리팹이므로 Release 불필요)
        pool.Init(original, default(AsyncOperationHandle<GameObject>), count);
        pool.Root.SetParent(_root);

        _pool.Add(original.name, pool);
    }

    // 가져오기 (Instantiate 대체)
    public Poolable Pop(GameObject original, Transform parent = null)
    {
        // 풀이 없으면 즉석에서 만듦
        if (_pool.ContainsKey(original.name) == false)
            CreatePool(original);

        return _pool[original.name].Pop(parent);
    }

    //  변경: GameObject 대신 string으로 받음
    public Poolable Pop(string prefabName, Transform parent = null)
    {
        if (!_pool.ContainsKey(prefabName))
        {
            Debug.LogError($"Pool에 '{prefabName}'이 없습니다. CreatePool을 먼저 호출하세요!");
            return null;
        }

        return _pool[prefabName].Pop(parent);
    }

    // 반납하기 (Destroy 대체)
    public void Push(Poolable poolable)
    {
        string name = poolable.gameObject.name;
        if (_pool.ContainsKey(name) == false)
        {
            Object.Destroy(poolable.gameObject);
            return;
        }

        _pool[name].Push(poolable);
    }

    // 씬 이동 시 풀 초기화 필요하면 사용
    public void Clear()
    {
        foreach (var pool in _pool.Values)
            pool.Release(); // 핸들 해제

        foreach (Transform child in _root)
            Object.Destroy(child.gameObject);

        _pool.Clear();
    }



}
