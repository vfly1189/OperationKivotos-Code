using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Inventory : UI_PopUp, IItemSlotHandler
{
    [SerializeField] private Transform _contentParent; // ScrollView의 Content
    [SerializeField] private Button[] _tabButtons; // 0:장비, 1:소비, 2:재료

    private ItemCategory _currentCategory = ItemCategory.Equipment;
    private List<UI_ItemSlot> _activeSlots = new List<UI_ItemSlot>();

    public override void Init()
    {
        base.Init();

        // 1. 탭 버튼 이벤트 연결
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int index = i;
            _tabButtons[i].onClick.AddListener(() => OnClickTab((ItemCategory)index));
        }

        // 2. InventoryManager의 갱신 이벤트 구독
        Managers.Inventory.OnInventoryUpdated -= RefreshUI;
        Managers.Inventory.OnInventoryUpdated += RefreshUI;

        Managers.UI.PreloadTooltip().Forget();

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

        var invenArray = Managers.Inventory.Inventory[category];

        // 최초 1회만 슬롯 미리 생성 (오브젝트 풀링)
        if (_activeSlots.Count == 0)
        {
            for (int i = 0; i < Managers.Inventory._maxSlotCount; i++)
            {
                UI_ItemSlot slot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _contentParent);
                _activeSlots.Add(slot);
            }
        }

        // 배열 데이터를 기반으로 갱신
        for (int i = 0; i < Managers.Inventory._maxSlotCount; i++)
        {
            _activeSlots[i].gameObject.SetActive(true);
            _activeSlots[i].SetInfo(invenArray[i], category, i);

            _activeSlots[i].SetHandler(this);         
        }
    }

    //인벤토리에서 더블클릭 = 장착
    public async void OnSlotDoubleClicked(UI_ItemSlot slot)
    {
        if (slot.CurrentCategory == ItemCategory.Equipment)
        {
            Managers.Equipment.Equip(slot.SlotIndex);
            Managers.UI.RefreshItemTooltip();
        }

        await UniTask.CompletedTask; 
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
            Managers.UI.ShowItemTooltip(slot.CurrentSlotData, screenPos);
        }
    }

    public void OnSlotPointerExit(UI_ItemSlot slot)
    {
        Managers.UI.HideItemTooltip();
    }


    private void OnDestroy()
    {
        // 이벤트 구독 해제 필수
        if (Managers.Inventory != null)
        {
            Managers.Inventory.OnInventoryUpdated -= RefreshUI;
        }
        // 2. 풀링되는 슬롯들의 콜백 초기화 (메모리 누수 및 오작동 방지)
        if (_activeSlots != null)
        {
            foreach (UI_ItemSlot slot in _activeSlots)
            {
                if (slot != null)
                {
                    slot.SetHandler(null); //풀 반환 시 반드시 초기화
                }
                Managers.Resource.Destroy(slot.gameObject);
            }
        }
    }
}
