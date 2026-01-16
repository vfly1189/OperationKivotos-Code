using UnityEngine;

public class Managers : MonoBehaviour
{
    static Managers s_instance; // 유일성 보장된다.
    static Managers Instance { get { Init(); return s_instance; } } // 유일성 보장된다.

    #region Core Manager
    InputManager _input = new InputManager();

    public static InputManager Input { get { return Instance._input; } }
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
            //s_instance._sound.Init();
        }
    }

    public static void Clear()
    {
        Input.Clear();
    }
}
