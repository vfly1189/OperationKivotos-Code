using UnityEngine;
// [추가] UniTask
using Cysharp.Threading.Tasks;
using NUnit.Framework.Constraints;

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
    SectorManager _sector = new SectorManager();
    InventoryManager _inventory = new InventoryManager();
    EquipmentManager _equipment = new EquipmentManager();
    SaveManager _save = new SaveManager();
    DropManager _drop = new DropManager();
    DungeonManager _dungeon = new DungeonManager();
    GameSessionContext _context = new GameSessionContext();
    FieldManager _field = new FieldManager();

    public static InputManager Input { get { return Instance._input; } }
    public static PartyManager Party { get { return Instance._party; } }
    public static PoolManager Pool { get { return Instance._pool; } }
    public static ResourceManager Resource { get { return Instance._resource; } }
    public static SceneManagerEx SceneEx { get { return Instance._scene; } }
    public static SoundManager Sound { get { return Instance._sound; } }
    public static UIManager UI { get { return Instance._ui; } }
    public static DataManager Data { get { return Instance._data; } }
    public static WalletManager Wallet { get { return Instance._wallet; } }
    public static SectorManager Sector { get { return Instance._sector; } }
    public static InventoryManager Inventory { get { return Instance._inventory; } }
    public static EquipmentManager Equipment { get { return Instance._equipment; } }
    public static SaveManager Save { get { return Instance._save; } }
    public static DropManager Drop { get { return Instance._drop; } }
    public static DungeonManager Dungeon {  get { return Instance._dungeon; } }
    public static GameSessionContext Context { get { return Instance._context; } }
    public static FieldManager Field { get { return Instance._field; } }
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
        // 씬 테이블 로드와 데이터 로드를 병렬로 동시에 처리하여 속도 최적화
        await UniTask.WhenAll(
            _scene.InitAsync(),
            _data.InitAsync()
        );

        Debug.Log("All Managers Async Initialization Complete");
    }

    void Update()
    {
        _input.OnUpdate();

        if (_party != null && _party.PlayerController != null)
        {
            _party.PlayerController.OnUpdate();
        }
    }

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
