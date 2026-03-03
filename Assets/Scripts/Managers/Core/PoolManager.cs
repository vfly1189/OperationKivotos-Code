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
     
        public void Init(GameObject original, int count = 5)
        {
            Original = original;
            //_handle = handle; // 핸들 저장
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

            poolable.IsUsing = true;
            return poolable;
        }


        // [수정 2] Addressable Release 삭제 및 유니티 기본 파괴만 수행
        public void DestroyPool()
        {
            if (Root != null)
            {
                Object.Destroy(Root.gameObject); // 하위 자식(풀링된 오브젝트)들까지 싹 다 날아감
            }
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

   

    public void CreatePool(GameObject original, int count = 10)
    {
 
        if (original == null || _pool.ContainsKey(original.name))
            return;

        Pool pool = new Pool();
        pool.Init(original, count);
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
        {
            pool.DestroyPool(); // 생성해둔 인스턴스들 물리적 파괴
        }
        _pool.Clear();
    }



}
