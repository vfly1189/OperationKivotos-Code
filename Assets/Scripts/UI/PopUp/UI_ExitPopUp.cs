using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ExitPopUp : UI_PopUp
{
    [Header("Popup Elements")]
    [SerializeField] 
    private TextMeshProUGUI _messageText;
    [SerializeField] 
    private Button _yesButton;
    [SerializeField] 
    private Button _noButton;

    [Header("Background")]
    [SerializeField] 
    private Image _backgroudImage;

    private Action _onConfirm;
    private Action _onCancel;

    public override void Init()
    {
        base.Init();

        // 버튼 이벤트 바인딩
        if (_yesButton != null)
            _yesButton.onClick.AddListener(OnYesClicked);

        if (_noButton != null)
            _noButton.onClick.AddListener(OnNoClicked);
    }

    // 외부에서 콜백 설정
    public void SetCallbacks(Action onConfirm, Action onCancel = null)
    {
        _onConfirm = onConfirm;
        _onCancel = onCancel;
    }

    private void OnYesClicked()
    {
        Debug.Log("Exit Confirmed");
        _onConfirm?.Invoke();

        // 게임 종료
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    private void OnNoClicked()
    {
        Debug.Log("Exit Cancelled");
        _onCancel?.Invoke();
        ClosePopupUI();
    }

    private void OnDestroy()
    {

    }
}
