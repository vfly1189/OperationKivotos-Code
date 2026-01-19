using UnityEngine;
using UnityEngine.EventSystems;

public abstract class BaseScene : MonoBehaviour
{
    public Define.Scene _sceneType { get; protected set; } = Define.Scene.Unknown;

    void Awake()
    {
        Init();
    }

    protected virtual void Init()
    {
        // EventSystem은 한 번만 생성
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = Managers.Resource.Instantiate("UI/EventSystem");
            eventSystem.name = "@EventSystem";
            DontDestroyOnLoad(eventSystem); //
        }
    }

    public abstract void Clear(); // 씬이 바뀔 때 날려야 할 것들 정리
}
