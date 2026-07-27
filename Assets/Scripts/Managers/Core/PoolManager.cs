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

        // [Phase 4] 이 풀이 붙잡은 원본 프리팹의 레지스트리 키. 부류 A(사전 로드)만 채워짐.
        string _sourceKey;
        // [Phase 4] 현재 Pop되어 아직 반환 안 된(IsUsing) 인스턴스 수 — 정리 시 누수 탐지용.
        int _activeCount;

        public void Init(GameObject original, string sourceKey, int count = 5)
        {
            Original = original;

            // [Phase 4] 원본 프리팹 핸들의 refCount 티켓을 풀당 한 장 획득한다.
            //  풀이 살아있는 동안 이 프리팹이 언로드되지 못하게 막는 잠금(= //_handle 주석의 실체).
            //  부류 B(레지스트리에 없는 SO 직접 참조)면 AcquirePoolRef가 false — 잡을 것이 없어 무시.
            _sourceKey = sourceKey;
            Managers.Resource.AcquirePoolRef(_sourceKey);

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

            // [Phase 4] 실제 사용 중이던(Pop된) 것만 카운트 감소. Init의 초기 채우기(IsUsing=false)는 제외.
            if (poolable.IsUsing) _activeCount--;

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
            _activeCount++;   // [Phase 4] 사용 중 인스턴스 +1
            return poolable;
        }


        // [Phase 4] 인스턴스 파괴 + 원본 프리팹 티켓 반납을 한 지점에서 함께 정산.
        public void DestroyPool()
        {
            // 신규 검증 지표(에디터/개발 빌드 전용): 스폰만 하고 회수 안 된 인스턴스 조기 탐지.
            //  주의 — Pop된 인스턴스는 씬의 자식이라 씬 언로드로 파괴돼도 Push를 안 거쳐 _activeCount가 남는다.
            //  따라서 "진입→미스폰→퇴장" 통제 시나리오에서 0인지로 검증한다(실제 플레이 중 인플라이트는 정상).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_activeCount > 0)
                GameLog.LogWarning($"[PoolLeak] '{Original.name}' 미반환 인스턴스 {_activeCount}개 — 통제 시나리오라면 회수 누락 의심");
#endif

            // 티켓은 Root 파괴 여부와 무관하게 반드시 반납한다.
            //  DDOL 제거로 씬 언로드가 Root를 먼저 파괴할 수 있으므로 null 가드에 묶지 않는다.
            Managers.Resource.ReleasePoolRef(_sourceKey);

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
        // [Phase 4] DontDestroyOnLoad 제거 — 풀 루트가 씬과 함께 파괴되어 인스턴스가 씬을 넘어 영생하지 않는다.
        //  씬 언로드로 파괴됐을 수 있으므로 실제 재생성은 EnsureRoot()가 필요 시점에 담당한다.
        EnsureRoot();
    }

    // [Phase 4] _root가 씬 언로드로 파괴됐으면 새로 만든다. Unity의 == null은 파괴된 오브젝트도 참으로 본다.
    void EnsureRoot()
    {
        if (_root == null)
            _root = new GameObject { name = "@Pool_Root" }.transform;
    }

    // sourceKey: 원본 프리팹의 레지스트리 키(부류 A). null이면 티켓 없이 생성(부류 B: 지연 자동 생성 등).
    public void CreatePool(GameObject original, string sourceKey = null, int count = 10)
    {
        if (original == null || _pool.ContainsKey(original.name))
            return;

        EnsureRoot();   // 씬 전환 후 첫 풀 생성 시 루트 복구

        Pool pool = new Pool();
        pool.Init(original, sourceKey, count);
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
            GameLog.LogError($"Pool에 '{prefabName}'이 없습니다. CreatePool을 먼저 호출하세요!");
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
