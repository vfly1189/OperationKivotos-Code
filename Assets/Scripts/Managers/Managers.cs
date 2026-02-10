using UnityEngine;

public class Managers : MonoBehaviour
{
    static Managers s_instance; // 유일성 보장된다.
    static Managers Instance { get { Init(); return s_instance; } } // 유일성 보장된다.

    private CurrentGameDataSO _currentGameContext = null;
    public static CurrentGameDataSO Context
    {
        get { return Instance._currentGameContext; }
    }

    #region Core Manager
    //GameManager _game = new GameManager();
    InputManager _input = new InputManager();
    PartyManager _party;
    PoolManager _pool = new PoolManager();
    ResourceManager _resource = new ResourceManager();
    SceneManagerEx _scene = new SceneManagerEx();
    SoundManager _sound = new SoundManager();
    UIManager _ui = new UIManager();

    //public static GameManager Game { get { return Instance._game; } }
    public static InputManager Input { get { return Instance._input; } }
    public static PartyManager Party { get { return Instance._party; } }
    public static PoolManager Pool { get { return Instance._pool; } }   
    public static ResourceManager Resource { get { return Instance._resource; } }
    public static SceneManagerEx SceneEx { get { return Instance._scene; } }
    public static SoundManager Sound { get { return Instance._sound; } }    
    public static UIManager UI { get { return Instance._ui; } }
    #endregion

    void Start()
    {
        Init();
    }

    void Update()
    {
        _input.OnUpdate();

        // PlayerController의 Update 로직 호출
        if (_party != null && _party.PlayerController != null)
        {
            _party.PlayerController.OnUpdate();
        }
    }

    static void Init()
    {
        // Initialize
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

            s_instance._currentGameContext = Resources.Load<CurrentGameDataSO>("Data/CurrentGameData/CurrentGameData");

            if (s_instance._currentGameContext == null)
            {
                Debug.LogError("CurrentGameDataSO를 찾을 수 없습니다! Resources/Data 폴더에 있는지 확인하세요.");
            }
            else
            {
                // 로드 성공 시 초기화 (선택사항)
                s_instance._currentGameContext.Clear();
            }

            s_instance._party = new PartyManager(s_instance);
           
            s_instance._pool.Init();
            s_instance._sound.Init();

            Application.targetFrameRate = 144; // 60프레임 고정
        }
    }


    void OnDestroy()
    {
        // PartyManager 정리
        if (_party != null)
        {
            _party.Dispose();
        }
    }

    public static void Start_Coroutine(System.Collections.IEnumerator routine)
    {
        Instance.StartCoroutine(routine);
    }

    public static void Clear()
    {
        SceneEx.Clear();
    }
}
