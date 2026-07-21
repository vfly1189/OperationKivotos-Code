using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.U2D;
using UnityEngine.UI;

public class UIManager
{
    int _order = 10;

    private GameObject _root = null;
    Stack<UI_PopUp> _popupStack = new Stack<UI_PopUp>();
    UI_Scene _sceneUI = null;

    private Canvas _canvasScene;    // 씬마다 고정으로 뜨는 UI들
    private Canvas _canvasPopup;    // popup들
    private Canvas _canvasSystem;   // order 100 이상의 절대로 먼저 보여져야 되는것들...
    private Canvas _canvasWorld;

    private bool _isLoadingPopup = false;
    public bool IsPopupOpen => _popupStack.Count > 0 || _isLoadingPopup;

    public GameObject Root
    {
        get
        {
            if (_root == null)
            {
                _root = GameObject.Find("@UI_Root");
                if (_root == null)
                {
                    _root = new GameObject { name = "@UI_Root" };
                    Object.DontDestroyOnLoad(_root);
                }
            }
            return _root;
        }
    }

    // =========================================================
    // 역할별 캔버스를 자동으로 찾아오거나 생성하는 프로퍼티
    // =========================================================
    public Canvas CanvasScene => GetOrMakeCanvas(ref _canvasScene, "@Canvas_Scene", 0);
    public Canvas CanvasWorld => GetOrMakeCanvas(ref _canvasWorld, "@Canvas_World", 5);
    public Canvas CanvasPopup => GetOrMakeCanvas(ref _canvasPopup, "@Canvas_Popup", 10);
    public Canvas CanvasSystem => GetOrMakeCanvas(ref _canvasSystem, "@Canvas_System", 100);

    private Canvas GetOrMakeCanvas(ref Canvas canvasField, string name, int sortOrder)
    {
        if (canvasField != null) return canvasField;

        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject { name = name };
            go.transform.SetParent(Root.transform, false);
        }

        canvasField = Util.GetOrAddComponent<Canvas>(go);
        canvasField.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasField.overrideSorting = true;
        canvasField.sortingOrder = sortOrder;


        CanvasScaler scaler = Util.GetOrAddComponent<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); // 프로젝트 해상도에 맞게 수정

        Util.GetOrAddComponent<GraphicRaycaster>(go);

        return canvasField;
    }



    // =========================================================
    // Canvas 세팅 (개별 팝업/씬 UI용 Nested Canvas)
    // =========================================================
    public void SetCanvas(GameObject go, bool sort = true)
    {
        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);

        // 중첩 Canvas는 부모의 RenderMode를 상속받으므로 여기서 수정하면 에러발생
        // canvas.renderMode = RenderMode.ScreenSpaceOverlay; <- 제거됨
        canvas.overrideSorting = true;
        canvas.sortingOrder = sort ? _order++ : 0;

        // 클릭 이벤트를 받기 위해 필수
        Util.GetOrAddComponent<GraphicRaycaster>(go);
    }

    public async UniTask<T> MakeSubItemAsync<T>(string addressableKey, Transform parent = null, CancellationToken token = default) where T : UI_Base
    {
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey, token: token);
        if (prefab == null) return null;

        GameObject go = Managers.Resource.Instantiate(prefab, parent);
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;

        return Util.GetOrAddComponent<T>(go);
    }
    public async UniTask<T> ShowPopupUIAsync<T>(string addressableKey = null) where T : UI_PopUp
    {
        if (_isLoadingPopup) return null;
        _isLoadingPopup = true;

        try
        {
            if (string.IsNullOrEmpty(addressableKey))
                addressableKey = typeof(T).Name;

            ResourceMetrics.BeginUIOpen(addressableKey); // [Phase 0.5 계측] 오픈 레이턴시 + 히치 관찰 시작

            // [Phase 3a] isGlobal 제거 → 기본 Scene 스코프(씬 전환 시 자동 회수). 팝업 프리팹 영구 상주 해소.
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey);
            if (prefab == null) return null;

            // 하드코딩된 @GameSceneCanvas 대신 CanvasPopup 아래에 배치
            GameObject go = Managers.Resource.Instantiate(prefab, CanvasPopup.transform);
            go.transform.SetParent(CanvasPopup.transform, false);
            go.SetActive(true);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            T popup = Util.GetOrAddComponent<T>(go);
            _popupStack.Push(popup);
            Managers.Input.PushContext(InputContext.UI);   // 팝업 열림 → 게임플레이 입력 차단

            GameLog.Log($"팝업 스택 : {_popupStack.Count}");

            SetCanvas(go, true);

            ResourceMetrics.MarkUIShown(addressableKey); // [Phase 0.5 계측] "표시됨" 마킹 (요청→표시 구간 확정)

            return popup;
        }
        finally
        {
            _isLoadingPopup = false;   // [Phase 3a] 실패·예외에도 반드시 복구 (팝업 영구 차단 버그 방지)
        }
    }


    // =========================================================
    // 팝업 닫기
    // =========================================================
    public void ClosePopupUI(UI_PopUp popup)
    {
        if (_popupStack.Count == 0) return;

        if (_popupStack.Peek() != popup)
        {
            GameLog.LogWarning("[UIManager] ClosePopupUI 실패 - 가장 위에 있는 팝업이 아닙니다.");
            return;
        }

        ClosePopupUI();
    }

    public void ClosePopupUI()
    {
        if (_popupStack.Count == 0) return;

        UI_PopUp popup = _popupStack.Pop();
        Managers.Input.PopContext();   // 팝업 닫힘 → 이전 컨텍스트로 복귀

        // 팝업이 닫힐 때 혹시 열려있을지 모르는 툴팁을 무조건 함께 끕니다.
        //HideItemTooltip();

        // 툴팁 끄는 로직은 정적 함수로 호출
        UI_ItemInfo.HideTooltip();

        // ResourceManager.Destroy로 위임 (풀링 여부 자동 처리)
        Managers.Resource.Destroy(popup.gameObject);

        _order--;
    }

    public void CloseAllPopupUI()
    {
        while (_popupStack.Count > 0)
            ClosePopupUI();

    }

    // =========================================================
    // SceneUI 세팅 (씬마다 고정으로 떠있는 UI, 예: UI_GameScene)
    // =========================================================
    public T ShowSceneUI<T>(string addressableKey = null) where T : UI_Scene
    {
        if (string.IsNullOrEmpty(addressableKey))
            addressableKey = typeof(T).Name;

        GameLog.Log($"SelectScene : {addressableKey}");
        // [수정] Root가 아니라 CanvasScene 아래에 배치해야 함
        GameObject go = Managers.Resource.Instantiate(addressableKey, Vector3.zero, Quaternion.identity, CanvasScene.transform);
        if (go == null) return null;

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        _sceneUI = Util.GetOrAddComponent<T>(go);
        SetCanvas(go, sort: false); // SceneUI는 팝업 뒤에 있어야 하므로 고정 order(0)

        return _sceneUI as T;
    }

    public void SetActiveSystemCanvas(bool value)
    {
        _canvasSystem.gameObject.SetActive(value);
    }

    public void SetActiveWorldCanvas(bool value)
    {
        _canvasWorld.gameObject.SetActive(value);
    }

    public bool IsOpened<T>() where T : UI_PopUp
    {
        // 스택을 순회하며 타입이 T와 일치하는 것이 있는지 검사
        foreach (var popup in _popupStack)
        {
            if (popup is T)
                return true;
        }
        return false;
    }


    // =========================================================
    // 정리
    // =========================================================
    public void Clear()
    {
        CloseAllPopupUI();

       
        if (_sceneUI != null)
        {
            Managers.Resource.Destroy(_sceneUI.gameObject);
            _sceneUI = null;
        }

        _order = 10;
    }
}
