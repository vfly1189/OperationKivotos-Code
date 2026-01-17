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
        // 모든 씬 공통 초기화 로직
        // 예: EventSystem이 없으면 생성해준다 (UI 클릭을 위해 필수)
        Object obj = GameObject.FindAnyObjectByType<EventSystem>();

        //TODO
        //이벤트 시스템이 없다면 생성해줘야됨. -> UI를 위해 필수.
        //if (obj == null)
        //    Managers.Resource.Instantiate("UI/EventSystem").name = "@EventSystem";
    }

    public abstract void Clear(); // 씬이 바뀔 때 날려야 할 것들 정리
}
