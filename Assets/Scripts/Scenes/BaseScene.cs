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
        //Managers.Input.OnEscapePressed -= OnEscapeEvent;
        //Managers.Input.OnEscapePressed += OnEscapeEvent;
    }

    public virtual void Clear()
    {
        Managers.Resource.Clear();
        //Managers.Input.OnEscapePressed -= OnEscapeEvent;
    }

    //// 델리게이트에 직접 연결되는 함수 (sealed 역할)
    //private void OnEscapeEvent()
    //{
    //    HandleEscape(); // 실제 다형성 발동 지점
    //}

    //// 자식 클래스에서 덮어쓸 로직
    //protected virtual void HandleEscape()
    //{
    //    if (Managers.UI.IsPopupOpen)
    //    {
    //        Managers.UI.ClosePopupUI();
    //    }
    //}
}
