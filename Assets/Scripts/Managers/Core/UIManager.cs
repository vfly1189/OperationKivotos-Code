using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class UIManager
{
    int _order = 10;

    private GameObject _root = null;

    Stack<UI_PopUp> _popupStack = new Stack<UI_PopUp>();
    UI_Scene _sceneUI = null;

    //팝업 열림 여부 확인
    public bool IsPopupOpen => _popupStack.Count > 0;


    // Addressables 프리팹 로드 시 생성된 Handle을 저장해둘 딕셔너리 (메모리 해제용)
    private Dictionary<UI_PopUp, AsyncOperationHandle<GameObject>> _popupHandles = 
        new Dictionary<UI_PopUp, AsyncOperationHandle<GameObject>>();

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

    public void SetCanvas(GameObject go, bool sort = true)
    {
        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        if (sort)
        {
            canvas.sortingOrder = _order;
            _order++;
        }
        else
        {
            canvas.sortingOrder = 0;
        }

    }


    public T MakeSubItem<T>(GameObject prefab, Transform parent = null) where T : UI_Base
    {
        // 1. 프리팹 인스턴스화
        GameObject go = Object.Instantiate(prefab);

        // 2. 부모 설정
        if (parent != null)
        {
            go.transform.SetParent(parent);
        }
        else
        {
            // 부모가 없으면 보통 SceneUI(전체화면 캔버스) 밑으로 가야 함.
            // 만약 현재 _sceneUI가 있다면 그리로, 없다면 Root나 임시 캔버스 찾기
            if (_sceneUI != null)
                go.transform.SetParent(_sceneUI.transform);
            else
            {
                // 씬에 있는 캔버스 찾아서 붙이기 (안전장치)
                Canvas canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas != null) go.transform.SetParent(canvas.transform);
            }
        }

        // 3. 스케일 초기화 (필수)
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;

        return Util.GetOrAddComponent<T>(go);
    }

    public T ShowPopupUI<T>(GameObject prefab) where T : UI_PopUp
    {
        // 1. 프리팹 인스턴스화
        GameObject go = Object.Instantiate(prefab);

        // 2. 이름 정리 (선택 사항, (Clone) 제거)
        // go.name = prefab.name; 

        // 3. UI 컴포넌트 가져오기 / 붙이기
        T popup = Util.GetOrAddComponent<T>(go);
        _popupStack.Push(popup);

        // 4. 부모 설정
        go.transform.SetParent(Root.transform);

        // 5. 스케일 초기화 (UI가 캔버스 밑으로 들어갈 때 가끔 꼬이는 경우 방지)
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;

        return popup;
    }

    
    public void ClosePopupUI(UI_PopUp popup)
    {
        if (_popupStack.Count == 0)
            return;

        if (_popupStack.Peek() != popup)
        {
            Debug.Log("Close Popup Failed!");
            return;
        }

        ClosePopupUI();
    }

    public void ClosePopupUI()
    {
        if (_popupStack.Count == 0)
            return;

        UI_PopUp popup = _popupStack.Pop();
        Managers.Resource.Destroy(popup.gameObject);

        // 2. Addressable Handle이 있다면 메모리 해제
        if (_popupHandles.TryGetValue(popup, out AsyncOperationHandle<GameObject> handle))
        {
            Addressables.Release(handle);
            _popupHandles.Remove(popup);
        }

        popup = null;
        _order--;
    }

    public void CloseAllPopupUI()
    {
        while (_popupStack.Count > 0)
            ClosePopupUI();
    }

    public void Clear()
    {
        CloseAllPopupUI();
        _sceneUI = null;
        _root = null;
    }


    // [추가] Addressable 키(이름)로 팝업 띄우기 (비동기)
    public async Task<T> ShowPopupUIAsync<T>(string addressableKey = null) where T : UI_PopUp
    {
        // 키가 안 주어지면 클래스 이름(예: "UI_WeaponUpgrade")을 키로 사용
        if (string.IsNullOrEmpty(addressableKey))
            addressableKey = typeof(T).Name;

        // 1. 프리팹 비동기 로드
        var handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject prefab = handle.Result;

            // 2. 기존 로직을 재활용하여 인스턴스화 및 셋업
            // (동기 ShowPopupUI의 1~5번 로직을 그대로 수행하는 내부 함수 호출)
            T popup = SetupPopupPrefab<T>(prefab);

            // 3. 나중에 창을 닫을 때 메모리(Handle)를 해제하기 위해 딕셔너리에 저장
            _popupHandles.Add(popup, handle);

            return popup;
        }
        else
        {
            Debug.LogError($"[UIManager] Failed to load Addressable UI: {addressableKey}");
            return null;
        }
    }

    // 기존 ShowPopupUI와 코드가 겹치므로 공통 로직을 빼낸 헬퍼 함수
    private T SetupPopupPrefab<T>(GameObject prefab) where T : UI_PopUp
    {
        GameObject go = Object.Instantiate(prefab);
        T popup = Util.GetOrAddComponent<T>(go);
        _popupStack.Push(popup);

        go.transform.SetParent(Root.transform);
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;

        return popup;
    }

}
