using UnityEngine;

public class Managers : MonoBehaviour
{
    static Managers s_instance; // 유일성 보장된다.
    static Managers Instance { get { Init(); return s_instance; } } // 유일성 보장된다.

    #region Core Manager
    InputManager _input = new InputManager();
    ResourceManager _resource = new ResourceManager();
    SceneManagerEx _scene = new SceneManagerEx();
    SoundManager _sound = new SoundManager();
    UIManager _ui = new UIManager();
    public static InputManager Input { get { return Instance._input; } }
    public static ResourceManager Resource { get { return Instance._resource; } }
    public static SceneManagerEx Scene { get { return Instance._scene; } }
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

            //s_instance._data.Init();
            //s_instance._pool.Init();
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
        Input.Clear();
        Scene.Clear();
    }
}
