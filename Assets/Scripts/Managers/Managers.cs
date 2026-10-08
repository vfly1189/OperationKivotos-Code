using UnityEngine;
// [추가] UniTask
using Cysharp.Threading.Tasks;

public class Managers : MonoBehaviour
{
    static Managers s_instance;
    static Managers Instance { get { Init(); return s_instance; } }

    #region Core Manager
    InputManager _input = new InputManager();
    PartyManager _party;
    PoolManager _pool = new PoolManager();
    ResourceManager _resource = new ResourceManager();
    SceneManagerEx _scene = new SceneManagerEx();
    SoundManager _sound = new SoundManager();
    UIManager _ui = new UIManager();
    DataManager _data = new DataManager();
    WalletManager _wallet = new WalletManager();
    InventoryManager _inventory = new InventoryManager();
    EquipmentManager _equipment = new EquipmentManager();
    SaveManager _save = new SaveManager();
    DropManager _drop = new DropManager();
    DungeonManager _dungeon = new DungeonManager();
    GameSessionContext _context = new GameSessionContext();
    FieldManager _field = new FieldManager();
    SpawnerManager _spawner = new SpawnerManager();
    ActivationManager _activationManager = new ActivationManager();

    public static InputManager Input { get { return Instance._input; } }
    public static PartyManager Party { get { return Instance._party; } }
    public static PoolManager Pool { get { return Instance._pool; } }
    public static ResourceManager Resource { get { return Instance._resource; } }
    public static SceneManagerEx SceneEx { get { return Instance._scene; } }
    public static SoundManager Sound { get { return Instance._sound; } }
    public static UIManager UI { get { return Instance._ui; } }
    public static DataManager Data { get { return Instance._data; } }
    public static WalletManager Wallet { get { return Instance._wallet; } }
    public static InventoryManager Inventory { get { return Instance._inventory; } }
    public static EquipmentManager Equipment { get { return Instance._equipment; } }
    public static SaveManager Save { get { return Instance._save; } }
    public static DropManager Drop { get { return Instance._drop; } }
    public static DungeonManager Dungeon {  get { return Instance._dungeon; } }
    public static GameSessionContext Context { get { return Instance._context; } }
    public static FieldManager Field { get { return Instance._field; } }

    public static SpawnerManager Spawner { get { return Instance._spawner; } }

    public static ActivationManager Activation { get { return Instance._activationManager; } }
    #endregion

    // [핵심 1] 코루틴 Start 대신 일반 Start에서 Fire-and-forget 비동기 실행
    void Start()
    {
        Init();
        InitializeAsync().Forget();
    }

    // 데이터 비동기 초기화
    private async UniTaskVoid InitializeAsync()
    {
        await UniTask.WhenAll(
            _scene.InitAsync(),
            _data.InitAsync()
        );

        GameLog.Log("All Managers Async Initialization Complete");
    }

    void Update()
    {
        _input.OnUpdate();
        _spawner.SampleMetrics();        // 계측: 직전 프레임 한 줄 (에디터 · 개발 빌드만)
        _activationManager.OnUpdate();   // 정책이 먼저 켜고/끄고 → 같은 프레임에 스포너 틱이 반영
        _spawner.OnUpdate();
        
        if (_party != null)
        {
            // 스킬 쿨타임은 AbilityRunner가 절대시각으로 소유 → 스왑아웃 멤버도 자동 경과(별도 tick 불필요).
            if (_party.PlayerController != null)
                _party.PlayerController.OnUpdate();
        }
    }

#if UNITY_EDITOR
    // 씬 뷰: 현재 활성화 정책의 범위 + 스포너 켜짐/꺼짐
    void OnDrawGizmos()
    {
        _activationManager?.DrawGizmos();
    }
#endif

    static void Init()
    {
        if (s_instance == null)
        {
            GameObject go = GameObject.Find("@Managers");

            if (go == null)
            {
                go = new GameObject { name = "@Managers" };
                go.AddComponent<Managers>();
            }

            DontDestroyOnLoad(go);
            s_instance = go.GetComponent<Managers>();

            s_instance._party = new PartyManager(s_instance.transform);
            s_instance._resource.Init();   // 레지스트리 생성 + Global/Scene 스코프 등록 (LoadAsync 전에 반드시)
            s_instance._pool.Init();
            s_instance._sound.Init();
            s_instance._wallet.Init();
            s_instance._inventory.Init();
            s_instance._equipment.Init();
            s_instance._save.Init();

            Application.targetFrameRate = 144;
        }
    }

    void OnApplicationQuit()
    {

    }

    void OnDestroy()
    {
        if (_party != null) _party.Dispose();
    }

    // [핵심 2] Start_Coroutine 삭제! 더 이상 코루틴 브릿지가 필요 없습니다.

    public static void Clear()
    {
        SceneEx.Clear();
        Field.Clear();
    }
}
