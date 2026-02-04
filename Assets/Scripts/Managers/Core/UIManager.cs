using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class UIManager
{
    int _order = 10;

    private GameObject _root = null;

    Stack<UI_PopUp> _popupStack = new Stack<UI_PopUp>();
    UI_Scene _sceneUI = null;

    //팝업 열림 여부 확인
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

}
