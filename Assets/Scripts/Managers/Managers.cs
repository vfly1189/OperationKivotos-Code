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
    //PartyManager _party = new PartyManager();
    PoolManager _pool = new PoolManager();
    ResourceManager _resource = new ResourceManager();
    SceneManagerEx _scene = new SceneManagerEx();
    SoundManager _sound = new SoundManager();
    UIManager _ui = new UIManager();

    //public static GameManager Game { get { return Instance._game; } }
    public static InputManager Input { get { return Instance._input; } }
    //public static PartyManager Party { get { return Instance._party; } }
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


            //s_instance._data.Init();
            s_instance._pool.Init();
            s_instance._sound.Init();
        }
    }


    // 외부(ResourceManager 등)에서 코루틴이 필요할 때 매니저에게 부탁하는 함수.
    public static void Start_Coroutine(System.Collections.IEnumerator routine)
    {
        // 내부에서는 Instance에 접근 가능하므로 실행 가능
        Instance.StartCoroutine(routine);
    }


    public static void Clear()
    {
        //Input.Clear();
        SceneEx.Clear();
    }
}
