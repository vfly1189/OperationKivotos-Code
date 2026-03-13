using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

    private UI_ItemInfo _currentTooltip = null;
    private bool _isLoadingTooltip = false;

    public bool IsPopupOpen => _popupStack.Count > 0;

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

    public async UniTask<T> MakeSubItemAsync<T>(string addressableKey, Transform parent = null) where T : UI_Base
    {
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey);
        if (prefab == null) return null;

        GameObject go = Managers.Resource.Instantiate(prefab, parent);
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;

        return Util.GetOrAddComponent<T>(go);
    }
    public async UniTask<T> ShowPopupUIAsync<T>(string addressableKey = null) where T : UI_PopUp
    {
        if (string.IsNullOrEmpty(addressableKey))
            addressableKey = typeof(T).Name;

        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey, isGlobal: true);
        if (prefab == null) return null;

        // [수정] 하드코딩된 @GameSceneCanvas 대신 CanvasPopup 아래에 배치
        GameObject go = Managers.Resource.Instantiate(prefab, CanvasPopup.transform);
        go.transform.SetParent(CanvasPopup.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        T popup = Util.GetOrAddComponent<T>(go);
        _popupStack.Push(popup);

        // [수정] 주석 해제. 팝업 정렬(Z-Order) 관리를 위해 호출
        SetCanvas(go, true);

        return popup;
    }


    // =========================================================
    // 팝업 닫기
    // =========================================================
    public void ClosePopupUI(UI_PopUp popup)
    {
        if (_popupStack.Count == 0) return;

        if (_popupStack.Peek() != popup)
        {
            Debug.LogWarning("[UIManager] ClosePopupUI 실패 - 가장 위에 있는 팝업이 아닙니다.");
            return;
        }

        ClosePopupUI();
    }

    public void ClosePopupUI()
    {
        if (_popupStack.Count == 0) return;

        UI_PopUp popup = _popupStack.Pop();

        // 팝업이 닫힐 때 혹시 열려있을지 모르는 툴팁을 무조건 함께 끕니다.
        HideItemTooltip();

        // ResourceManager.Destroy로 위임 (풀링 여부 자동 처리)
        Managers.Resource.Destroy(popup.gameObject);

        _order--;
    }

    public void CloseAllPopupUI()
    {
        while (_popupStack.Count > 0)
            ClosePopupUI();

        // 팝업이 닫힐 때 혹시 열려있을지 모르는 툴팁을 무조건 함께 끕니다.
        HideItemTooltip();
    }

    // =========================================================
    // SceneUI 세팅 (씬마다 고정으로 떠있는 UI, 예: UI_GameScene)
    // =========================================================
    public T ShowSceneUI<T>(string addressableKey = null) where T : UI_Scene
    {
        if (string.IsNullOrEmpty(addressableKey))
            addressableKey = typeof(T).Name;

        Debug.Log($"SelectScene : {addressableKey}");
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

    public async void ShowItemTooltip(InventorySlot slot, Vector2 screenPos)
    {
        // 1. 데이터가 비어있으면 띄우지 않음
        if (slot == null || slot.IsEmpty || _isLoadingTooltip) return;

        // 2. 툴팁 UI가 아직 없다면 비동기로 로드하여 CanvasPopup(또는 CanvasSystem) 최상단에 생성
        if (_currentTooltip == null)
        {
            _isLoadingTooltip = true; // 로딩 시작

            // CanvasSystem을 쓰면 다른 모든 팝업들보다 무조건 위에 그려집니다.
            _currentTooltip = await MakeSubItemAsync<UI_ItemInfo>("UI_ItemInfo", CanvasSystem.transform);

            // 툴팁은 클릭 이벤트를 받을 필요가 없으므로 Raycast Target을 꺼주는 것이 좋습니다.
            // (UI_ItemInfo 내부의 Init()에서 처리해도 됨)

            _isLoadingTooltip = false; // 로딩 완료

            // 로딩 중에 창이 닫혔거나 게임이 꺼진 경우를 위한 방어 코드
            if (_currentTooltip == null) return;
        }

        // 3. UI 활성화 및 정보 셋팅
        _currentTooltip.gameObject.SetActive(true);
        _currentTooltip.SetInfo(slot);

        // 4. 위치 조정 (마우스 위치로 이동)
        // RectTransformUtility를 사용하여 스크린 좌표(마우스)를 Canvas의 로컬 좌표로 변환합니다 [web:28, web:31].
        RectTransform tooltipRect = _currentTooltip.GetComponent<RectTransform>();
        RectTransform canvasRect = CanvasSystem.GetComponent<RectTransform>();

        // 화면 Space-Overlay일 경우 Camera는 null을 전달 [web:29].
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPoint))
        {
            // 마우스 커서에 정확히 겹치면 클릭을 방해할 수 있으므로, 우측 하단으로 약간 오프셋을 줍니다.
            localPoint += new Vector2(350f, 0);
            //localPoint += Vector2.zero;

            tooltipRect.localPosition = localPoint;
        }
    }

    public void HideItemTooltip()
    {
        if (_currentTooltip != null && _currentTooltip.gameObject.activeSelf)
        {
            _currentTooltip.gameObject.SetActive(false);
        }
    }

    public async UniTask PreloadTooltip()
    {
        if (_currentTooltip == null)
        {
            _isLoadingTooltip = true;
            _currentTooltip = await MakeSubItemAsync<UI_ItemInfo>("UI_ItemInfo", CanvasSystem.transform);
            _currentTooltip.gameObject.SetActive(false); // 일단 꺼둠
            _isLoadingTooltip = false;
        }
    }

    // UIManager.cs 안에 추가
    public void RefreshItemTooltip()
    {
        // 툴팁이 켜져있지 않다면 무시
        if (_currentTooltip == null || !_currentTooltip.gameObject.activeSelf) return;

        // 방금 아이템이 교체/소모되어 빈 슬롯이 되었을 수 있으므로 
        // 일단 무조건 툴팁을 끕니다.
        HideItemTooltip();

        // 끄고 난 뒤, 유니티의 EventSystem을 이용해 현재 마우스(포인터) 아래에 
        // 어떤 UI가 있는지 검사하여 다시 OnPointerEnter 이벤트를 발생시킵니다.
        // (마우스가 여전히 아이템 슬롯 위에 있다면 툴팁이 즉시 다시 켜짐)

        // 3. New Input System의 마우스 연결 상태를 체크합니다.
        if (Mouse.current == null) return;

        // 4. 최신 Input System의 마우스 좌표를 가져옵니다. (Vector2 반환)
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 5. 마우스 위치를 기반으로 UI Raycast를 쏩니다.
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = mousePos // <- 여기서 Input.mousePosition 대신 최신 좌표를 넣습니다.
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count > 0)
        {
            // 마우스 아래에 있는 첫 번째 UI 오브젝트를 가져옴
            GameObject hoveredObject = results[0].gameObject;

            // 그 오브젝트(또는 부모)에 UI_ItemSlot 컴포넌트가 있다면 Enter 이벤트를 수동 호출
            UI_ItemSlot slot = hoveredObject.GetComponentInParent<UI_ItemSlot>();
            if (slot != null)
            {
                slot.OnPointerEnter(pointerData);
            }

            // [추가] UI_EquipSlot 위에서 더블클릭으로 해제했을 때를 대비해 EquipSlot도 체크
            UI_EquipSlot equipSlot = hoveredObject.GetComponentInParent<UI_EquipSlot>();
            if (equipSlot != null)
            {
                equipSlot.OnPointerEnter(pointerData);
            }
        }
    }

    // =========================================================
    // 정리
    // =========================================================
    public void Clear()
    {
        CloseAllPopupUI();

        // [수정] 씬 전환 시 SceneUI 파괴 처리 추가
        if (_sceneUI != null)
        {
            Managers.Resource.Destroy(_sceneUI.gameObject);
            _sceneUI = null;
        }

        // [수정] _root = null 삭제. DDOL이므로 Root와 Canvas들은 유지되어야 함
        _order = 10;
    }
}
