using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;

public abstract class BaseScene : MonoBehaviour
{
    public Define.Scene _sceneType { get; protected set; } = Define.Scene.Unknown;

    protected GameObject _modelCamera;
    protected int _prevIndex = -1;
    protected List<GameObject> _infoModels = new List<GameObject>();

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
        //Managers.Resource.Clear();
        //Managers.Input.OnEscapePressed -= OnEscapeEvent;
    }

    


    protected virtual void HandleEscape()
    {
        if (Managers.UI.IsPopupOpen)
        {
            GameLog.Log("닫기 시작 ");
            Managers.UI.ClosePopupUI();
        }
        else
        {
            GameLog.Log("또 열기");
            ShowEscapeMenu().Forget();
        }
    }

    // 버튼 클릭 등의 이벤트에서 비동기를 띄울 때는 async UniTaskVoid 사용
    protected async UniTaskVoid ShowEscapeMenu()
    {
        //var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.exitPopup);
        UI_EscapeMenu popupPrefab = await Managers.UI.ShowPopupUIAsync<UI_EscapeMenu>("UI_EscapeMenu");
    }
}
