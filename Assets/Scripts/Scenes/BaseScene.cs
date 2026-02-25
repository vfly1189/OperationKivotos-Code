using System.Collections.Generic;
using System.IO;
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
            DontDestroyOnLoad(eventSystem);
        }
    }

    public virtual void Clear()
    {
        Managers.Resource.Clear();
    }
}
