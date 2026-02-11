using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

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
            Root = new GameObject().transform;
            Root.name = $"{original.name}_Pool_Root";

            for (int i = 0; i < count; i++)
                Push(Create());
        }

        Poolable Create()
        {
            GameObject go = Object.Instantiate(Original);
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

            // ★ Transform 초기화 (안전장치)
            // 1. 스케일 리셋 (가장 중요)
            poolable.transform.localScale = Vector3.one;

            // 2. 위치/회전 리셋 (필요하다면)
            //poolable.transform.localPosition = Vector3.zero;
            //poolable.transform.localRotation = Quaternion.identity;

            poolable.IsUsing = true;

            // 부모 설정
            if (parent == null)
                poolable.transform.SetParent(Managers.SceneEx.CurrentScene.transform);
            else
                poolable.transform.SetParent(parent);

            // 활성화는 ResourceManager에서 위치 잡은 뒤에 함
            // poolable.gameObject.SetActive(true); 
            return poolable;
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
    public void CreatePool(GameObject original, int count = 10)
    {
        //이미 풀이 존재하면 중복 생성하지 않고 리턴
        if (_pool.ContainsKey(original.name))
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
        foreach (Transform child in _root)
            Object.Destroy(child.gameObject);

        _pool.Clear();
    }



}
