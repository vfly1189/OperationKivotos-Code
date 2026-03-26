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
        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Input.OnEscapePressed += HandleEscape;
    }

    public virtual void Clear()
    {
        Managers.Resource.Clear();
        Managers.Input.OnEscapePressed -= HandleEscape;
    }

    private void HandleEscape()
    {
        if (Managers.UI.IsPopupOpen)
        {
            Managers.UI.ClosePopupUI();
        }
    }
}
