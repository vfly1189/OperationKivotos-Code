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

        SetItemIcon(itemData);
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

    private async void SetItemIcon(BaseItemData itemData)
    {
        if (itemData == null) return;

        // 1. 타입 패턴 매칭을 통해 아틀라스 키와 아이콘 이름 분기 처리
        (string atlasKey, string iconName) = itemData switch
        {
            // itemData가 EquipmentData 타입이면 equip 변수에 할당하고 블록 실행
            EquipmentData equip => ("EquipmentIconAtlas", equip.IconKey),

            // itemData가 ConsumableData 타입이면 cons 변수에 할당하고 블록 실행
            ConsumableData cons => ("ConsumablesAtlas", cons.IconKey),

            // itemData가 MaterialData 타입이면 mat 변수에 할당하고 블록 실행
            MaterialData mat => ("MaterialIconAtlas", mat.IconKey),

            // 어떤 타입에도 맞지 않거나 에러 방지용 (기본값)
            _ => ("CommonAtlas", itemData.IconKey)
        };

        // 2. 결정된 아틀라스와 이름으로 스프라이트 비동기 로드
        Sprite sprite = await Managers.Resource.GetSpriteFromAtlasAsync(atlasKey, iconName);

        // 3. UI 적용
        if (sprite != null && _itemIcon != null)
        {
            _itemIcon.sprite = sprite;
        }
    }

    private async void SetGradeBackGround(ItemGrade grade)
    {
        //Sprite bgSprite = await Managers.Resource.LoadAsync<Sprite>($"GradeBg_{grade}", isGlobal: true);
        Sprite bgSprite = await Managers.Resource.GetSpriteFromAtlasAsync("ItemGradeAtlas", $"GradeBg_{grade}");

        if (bgSprite != null && _itemGrade != null) _itemGrade.sprite = bgSprite;
    }

    private void OnDestroy()
    {
        _slotButton.onClick.RemoveAllListeners();
        _cancelButton.onClick.RemoveAllListeners();
    }
}
