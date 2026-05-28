using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class UI_EquipSlot : UI_Base, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _defaultBackGround;
    [SerializeField] private Image _itemGradeBackGround;
    [SerializeField] private Image _itemIcon;

    private EquipType _equipType;

    private InventorySlot _currentSlotData;

    public override void Init()
    {


    }

    public void SetType(EquipType equipType) { _equipType = equipType; }

    public void SetInfo(InventorySlot slotData)
    {
        _currentSlotData = slotData;

        // slotData 자체가 null일 때의 방어 코드가 필요합니다.
        if (slotData == null || slotData.IsEmpty)
        {
            _itemIcon.gameObject.SetActive(false);
            _itemGradeBackGround.gameObject.SetActive(false);
            return;
        }

        // 아이템 정보가 있을 때 셋팅
        _itemIcon.gameObject.SetActive(true);
        _itemGradeBackGround.gameObject.SetActive(true);

        BaseItemData itemData = Managers.Data.GetItemData(slotData.itemID, ItemCategory.Equipment);
        if (itemData != null)
        {
            SetItemIcon(itemData.IconKey);
            SetGradeBackGround(itemData.Grade);
        }
    }
    private async void SetItemIcon(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey)) return;

        Debug.Log($"Test : {iconKey}");
        // ResourceManager를 통해 비동기로 Sprite 로드
        //Sprite sprite = await Managers.Resource.LoadAsync<Sprite>(iconKey);
        Sprite icon = await Managers.Resource.GetSpriteFromAtlasAsync("EquipmentIconAtlas", iconKey);


        if (icon != null && _itemIcon != null)
        {
            _itemIcon.sprite = icon;
            _itemIcon.gameObject.SetActive(true);
        }
    }
    private async void SetGradeBackGround(ItemGrade grade)
    {
        // 등급에 맞는 Addressable Key 문자열 조합 (예: "Common_Gray", "Rare_Blue")
        string gradeKey = $"GradeBg_{grade.ToString()}"; // 예시
        Debug.Log($"GradeKey : {gradeKey}");
        //Sprite bgSprite = await Managers.Resource.LoadAsync<Sprite>(gradeKey);
        Sprite bgSprite = await Managers.Resource.GetSpriteFromAtlasAsync("ItemGradeAtlas", gradeKey);


        if (bgSprite != null && _itemGradeBackGround != null)
        {
            _itemGradeBackGround.sprite = bgSprite;
        }
    }

    // 2. 장착된 장비를 더블 클릭하여 해제할 때
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
        {
            if (_currentSlotData != null && !_currentSlotData.IsEmpty)
            {
                Debug.Log("EquipSlot OnPointerClick 호출!");
                Managers.Equipment.UnEquip(_equipType);
            }
        }
    }

    // 인벤토리 슬롯 (UI_ItemSlot) 내부의 이벤트
    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"OnPointerEnter 시작");
        if (_currentSlotData != null && !_currentSlotData.IsEmpty)
        {
            Debug.Log($"OnPointerEnter 툴팁 시작");

            // 툴팁 활성화 및 정보 셋팅
            //Managers.UI.ShowItemTooltip(_currentSlotData, eventData.position);
            UI_ItemInfo.ShowTooltip(_currentSlotData, eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //Managers.UI.HideItemTooltip();
        UI_ItemInfo.HideTooltip();
    }
}
