using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Inventory : UI_PopUp, IItemSlotHandler
{
    [SerializeField] private Transform _contentParent; // ScrollView의 Content
    [SerializeField] private Button[] _tabButtons; // 0:장비, 1:소비, 2:재료

    private ItemCategory _currentCategory = ItemCategory.Equipment;
    private List<UI_ItemSlot> _activeSlots = new List<UI_ItemSlot>();
    private bool _isRefreshing = false;
    public override void Init()
    {
        base.Init();

        ItemCategory[] tabCategories = { ItemCategory.Equipment, ItemCategory.Consumable, ItemCategory.Material };
        // 1. 탭 버튼 이벤트 연결
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int index = i;
            _tabButtons[i].onClick.AddListener(() => OnClickTab(tabCategories[index]));
        }

        // 2. InventoryManager의 갱신 이벤트 구독
        Managers.Inventory.OnInventoryUpdated -= RefreshUI;
        Managers.Inventory.OnInventoryUpdated += RefreshUI;

        //Managers.UI.PreloadTooltip().Forget();

        // 초기 화면 그리기
        RefreshUI(_currentCategory);
    }

    private void OnClickTab(ItemCategory category)
    {
        if (_currentCategory == category) return;

        _currentCategory = category;
        RefreshUI(_currentCategory);
    }

    private async void RefreshUI(ItemCategory category)
    {
        if (_currentCategory != category) return;
        if (_isRefreshing) return; // 중복 진입 차단
        _isRefreshing = true;

        var invenArray = Managers.Inventory.Inventory[category];

        if (_activeSlots.Count == 0)
        {
            for (int i = 0; i < Managers.Inventory._maxSlotCount; i++)
            {
                UI_ItemSlot slot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _contentParent);
                _activeSlots.Add(slot);
            }
        }

        (Func<InventorySlot, string> formatter, SlotSubTextStyle style) = category switch
        {
            ItemCategory.Equipment => (ItemSlotSubText.UpgradeLevel, SlotSubTextStyle.UpgradeLevel),
            ItemCategory.Material => (ItemSlotSubText.StackCount, SlotSubTextStyle.DefaultMaterial),
            ItemCategory.Consumable => (ItemSlotSubText.StackCount, SlotSubTextStyle.DefaultMaterial),
            _ => (ItemSlotSubText.None, SlotSubTextStyle.Default)
        };

        for (int i = 0; i < Managers.Inventory._maxSlotCount; i++)
        {
            _activeSlots[i].gameObject.SetActive(true);
            _activeSlots[i].SetSubTextFormatter(formatter);
            _activeSlots[i].SetSubTextStyle(style);
            _activeSlots[i].SetInfo(invenArray[i], category, i);
            _activeSlots[i].SetHandler(this);
        }

        _isRefreshing = false;
    }

    // 인벤토리에서 단순 클릭은 툴팁 갱신 정도
    public void OnSlotClicked(UI_ItemSlot slot)
    {
        // 필요 없으면 빈 구현이라도 명시
    }

    //인벤토리에서 더블클릭 = 장착
    public void OnSlotDoubleClicked(UI_ItemSlot slot)
    {
        if (slot.CurrentCategory == ItemCategory.Equipment)
        {
            Managers.Equipment.Equip(slot.SlotIndex);
            //Managers.UI.RefreshItemTooltip();
            UI_ItemInfo.RefreshItemTooltip();
        }
    }

    //인벤토리에서 드롭 = 슬롯 스왑
    public void OnSlotDrop(UI_ItemSlot from, UI_ItemSlot to)
    {
        Managers.Inventory.SwapItems(from.CurrentCategory, from.SlotIndex, to.SlotIndex);
    }

    public void OnSlotPointerEnter(UI_ItemSlot slot, Vector2 screenPos)
    {
        if (slot.CurrentSlotData != null && !slot.CurrentSlotData.IsEmpty)
        {
            // 툴팁 활성화 및 정보 셋팅
            //Managers.UI.ShowItemTooltip(slot.CurrentSlotData, screenPos);
            UI_ItemInfo.ShowTooltip(slot.CurrentSlotData, screenPos);
        }
    }

    public void OnSlotPointerExit(UI_ItemSlot slot)
    {
        //Managers.UI.HideItemTooltip();
        UI_ItemInfo.HideTooltip();
    }


    private void OnDestroy()
    {
        if (Managers.Inventory != null)
            Managers.Inventory.OnInventoryUpdated -= RefreshUI;

        if (_activeSlots == null) return;

        foreach (var slot in _activeSlots)
        {
            if (slot != null) slot.SetHandler(null);
        }
        foreach (var slot in _activeSlots)
        {
            if (slot != null) Managers.Resource.Destroy(slot.gameObject);
        }
        _activeSlots.Clear();
    }
}
