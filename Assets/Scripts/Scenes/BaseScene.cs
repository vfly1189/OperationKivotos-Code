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

    }

    public virtual void Clear()
    {
        Managers.Resource.Clear();
    }
}
