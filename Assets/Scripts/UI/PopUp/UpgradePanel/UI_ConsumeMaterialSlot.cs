using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_ConsumeMaterialSlot : UI_Base
{
    [SerializeField] Button _slotButton;
    [SerializeField] Image _itemGrade;
    [SerializeField] Image _itemIcon;

    // 클릭 시 실행할 콜백 이벤트
    private Action _onClickCallback;

    public override void Init()
    {
        _itemGrade.gameObject.SetActive(false);
        _itemIcon.gameObject.SetActive(false);

        _slotButton.onClick.AddListener(OnSlotClicked);
    }

    public void SetCallback(Action onClickAction)
    {
        _onClickCallback = onClickAction;
    }

    private void OnSlotClicked()
    {
        // 콜백이 등록되어 있다면 실행
        _onClickCallback?.Invoke();
    }

    private void OnDestroy()
    {
        _slotButton.onClick.RemoveAllListeners();
        _onClickCallback = null;
    }
}
