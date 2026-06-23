using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

public class SelectSceneCanvas : UI_Scene
{
    // ========================================================================
    // [2] UI 요소 그룹화 (버튼 및 표시부)
    // ========================================================================
    [System.Serializable]
    public class SchoolUIElements
    {
        public Button textButton;
        public Button imageButton;
    }

    [Header("UI Controls")]
    [SerializeField] private SchoolUIElements[] _schoolUIElements; // 0:아비도스, 1:게헨나, 2:밀레니엄
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _newStartButton;

    [Header("Display Info")]
    [SerializeField] private Image _schoolIcon;
    [SerializeField] private Image _schoolName;

    private SelectScene _scene; // Scene 참조
    private bool _isInit = false; // 중복 초기화 방지 플래그

    public event Action OnContinue;
    public event Action OnNewStart;

    // 2. UI_Base의 추상 메서드이자 UI_Scene의 Init을 오버라이드
    public override void Init()
    {
        if (_isInit) return; // Start()와 수동 호출이 겹치지 않게 방지

        base.Init(); // 부모(UI_Scene)의 Init 호출 (SetCanvas 등)

        // 버튼 리스너 연결 (Awake/Start 타이밍에 1번만 실행됨)
        for (int i = 0; i < _schoolUIElements.Length; i++)
        {
            int idx = i; // 클로저 이슈 방지를 위한 지역 변수 복사
            _schoolUIElements[i].imageButton.onClick.AddListener(() => OnClickSchool(idx));
            _schoolUIElements[i].textButton.onClick.AddListener(() => OnClickSchool(idx));
        }

        //_gameStartButton.onClick.AddListener(OnClickGameStart);
        _continueButton.onClick.AddListener(OnClickContinue);
        _newStartButton.onClick.AddListener(OnClickNewStart);
        
        _isInit = true;
    }

    // 3. 기존의 Init(SelectScene scene)을 Setup(데이터 주입용)으로 변경
    public void Setup(SelectScene scene)
    {
        _scene = scene;
    }

    private void OnClickSchool(int idx)
    {
        if (_scene != null)
            _scene.SelectSchool(idx);
    }
    private void OnClickContinue()
    {
        OnContinue?.Invoke();
        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game);     
    }

    private void OnClickNewStart()
    {
        OnNewStart?.Invoke();
        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game);
    }

    // 4. UpdateUIState는 기존 로직 그대로 유지
    public void UpdateUIState(int index, SchoolDataSO data)
    {
        // 1. 아이콘 변경
        if (data != null)
        {
            _schoolIcon.sprite = data.schoolIcon;
            _schoolName.sprite = data.schoolNameFont;
        }

        // 2. 버튼 활성/비활성 처리
        for (int i = 0; i < _schoolUIElements.Length; i++)
        {
            _schoolUIElements[i].imageButton.interactable = (i != index);
            _schoolUIElements[i].textButton.interactable = (i != index);
        }
    }
}
