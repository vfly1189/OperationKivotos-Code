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
            DontDestroyOnLoad(eventSystem); //
        }
    }

    // 각 씬마다 필요한 리소스 경로를 반환 (오버라이드 가능)
    protected virtual string[] GetRequiredResources()
    {
        return null;
    }

    // 리소스 가져오기
    public T GetResource<T>(string path) where T : UnityEngine.Object
    {
        // Managers.Resource는 캐시에 있으면 바로 주고, 없으면 로드함
        // 이미 LoadingScene에서 프리로드 했으므로 캐시에서 즉시 리턴될 것임
        return Managers.Resource.Load<T>(path);
    }

    public virtual void Clear()
    {
        Managers.Resource.Clear();
    }
}
