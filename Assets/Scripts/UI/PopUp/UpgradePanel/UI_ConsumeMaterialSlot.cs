using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ConsumeMaterialSlot : UI_Base
{
    [SerializeField] private Button _slotButton;
    [SerializeField] private Image _itemGrade;
    [SerializeField] private Image _itemIcon;

    [SerializeField] private TextMeshProUGUI _stackText;
    [SerializeField] private Button _cancelButton; // 강화재료 취소 버튼, 꺼져있으니 사용할거면 켜야됨

    // 클릭 시 실행할 콜백 이벤트
    private Action _onClickCallback;
    private Action _onCancelCallback;

    public override void Init()
    {
        _itemGrade.gameObject.SetActive(false);
        _itemIcon.gameObject.SetActive(false);
        _stackText.gameObject.SetActive(false);
        _cancelButton.gameObject.SetActive(false);

        _slotButton.onClick.AddListener(() => _onClickCallback?.Invoke());
        _cancelButton.onClick.AddListener(() => _onCancelCallback?.Invoke());
    }

    public void SetSlot(MaterialEntry entry)
    {
        if (entry == null || entry.IsEmpty) { ClearSlot(); return; }

        var category = entry.IsBook ? ItemCategory.Material : ItemCategory.Equipment;
        var itemData = Managers.Data.GetItemData(entry.Slot.itemID, category);

        SetItemIcon(itemData.IconKey);
        SetGradeBackGround(itemData.Grade);

        _itemGrade.gameObject.SetActive(true);
        _itemIcon.gameObject.SetActive(true);

        //  책만 스택 카운트 표시
        bool showStack = entry.IsBook && entry.Count > 1;
        _stackText.gameObject.SetActive(showStack);
        if (showStack) _stackText.text = $"x{entry.Count}";

        //  등록되면 취소 버튼 활성화
        _cancelButton.gameObject.SetActive(true);
    }

    public void ClearSlot()
    {
        _itemGrade.gameObject.SetActive(false);
        _itemIcon.gameObject.SetActive(false);
        _stackText.gameObject.SetActive(false);
        _cancelButton.gameObject.SetActive(false);
    }

    public void SetCallback(Action onClick, Action onCancel)
    {
        _onClickCallback = onClick;
        _onCancelCallback = onCancel;
    }

    private async void SetItemIcon(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey)) return;
        Sprite sprite = await Managers.Resource.LoadAsync<Sprite>(iconKey, isGlobal: true);
        if (sprite != null && _itemIcon != null) _itemIcon.sprite = sprite;
    }

    private async void SetGradeBackGround(ItemGrade grade)
    {
        Sprite bgSprite = await Managers.Resource.LoadAsync<Sprite>($"GradeBg_{grade}", isGlobal: true);
        if (bgSprite != null && _itemGrade != null) _itemGrade.sprite = bgSprite;
    }

    private void OnDestroy()
    {
        _slotButton.onClick.RemoveAllListeners();
        _cancelButton.onClick.RemoveAllListeners();
    }
}
