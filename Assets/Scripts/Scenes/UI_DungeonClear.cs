using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_DungeonClear : UI_Scene, IItemSlotHandler
{
    [SerializeField] private Button _dungeonClearConfirmButton;

    [SerializeField] private TextMeshProUGUI _creditText;
    [SerializeField] private TextMeshProUGUI _expText;

    [SerializeField] private Transform _rewardList;

    private List<UI_ItemSlot> _activeSlots = new List<UI_ItemSlot>();

    public override void Init()
    {
        base.Init();
        _dungeonClearConfirmButton.onClick.AddListener(Confirm);
    }

    //public async void SetInfo(int gainedCredit, int gainedExp, List<InventorySlot> droppedItems)
    //{
    //    _creditText.text = gainedCredit.ToString("N0");
    //    _expText.text = gainedExp.ToString("N0");

    //    foreach (var item in droppedItems)
    //    {
    //        if (item == null || item.IsEmpty) continue;

    //        // 1. 슬롯 비동기 생성
    //        UI_ItemSlot slot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _rewardList);

    //        // 2. 현재 아이템의 카테고리에 맞춰 포맷터와 스타일 결정 (루프 내부에서 처리!)
    //        var (formatter, style) = GetSlotStyleByCategory(item.itemCategory);

    //        // 3. 슬롯 초기화 및 데이터 주입
    //        slot.gameObject.SetActive(true);
    //        slot.SetSubTextFormatter(formatter);
    //        slot.SetSubTextStyle(style);
    //        slot.SetInfo(item, item.itemCategory, -1);
    //        slot.SetHandler(this);

    //        _activeSlots.Add(slot);
    //    }
    //}

    public async void SetInfo(int gainedCredit, int gainedExp, List<InventorySlot> droppedItems)
    {
        _creditText.text = gainedCredit.ToString("N0");
        _expText.text = gainedExp.ToString("N0");

        // [핵심 변경] 아이템 스택 처리 (LINQ 활용)
        List<InventorySlot> stackedItems = StackDroppedItems(droppedItems);

        foreach (var item in stackedItems) // [수정] stackedItems로 순회
        {
            if (item == null || item.IsEmpty) continue;

            // 1. 슬롯 비동기 생성
            UI_ItemSlot slot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _rewardList);

            // 2. 현재 아이템의 카테고리에 맞춰 포맷터와 스타일 결정
            var (formatter, style) = GetSlotStyleByCategory(item.itemCategory);

            // 3. 슬롯 초기화 및 데이터 주입
            slot.gameObject.SetActive(true);
            slot.SetSubTextFormatter(formatter);
            slot.SetSubTextStyle(style);
            slot.SetInfo(item, item.itemCategory, -1);
            slot.SetHandler(this);

            _activeSlots.Add(slot);
        }
    }

    // ========================================================
    //  아이템 스택 병합(Stacking) 로직
    // ========================================================
    private List<InventorySlot> StackDroppedItems(List<InventorySlot> rawItems)
    {
        List<InventorySlot> result = new List<InventorySlot>();

        // 1. 장비(Equipment)는 옵션이 다르므로 합치지 않고 그대로 결과 리스트에 추가
        var equipments = rawItems.Where(i => i.itemCategory == ItemCategory.Equipment);
        result.AddRange(equipments);

        // 2. 장비가 아닌 아이템(Material, Consumable 등)은 ItemID 기준으로 그룹화(GroupBy)
        var stackableItems = rawItems.Where(i => i.itemCategory != ItemCategory.Equipment);
        var groupedItems = stackableItems.GroupBy(i => i.itemID);

        // 3. 그룹화된 아이템들의 Amount(수량)를 합쳐서 하나의 InventorySlot으로 새로 만듦
        foreach (var group in groupedItems)
        {
            // group.Key = itemID
            // group.Sum(i => i.Amount) = 같은 아이템들의 갯수 총합

            InventorySlot stackedSlot = new InventorySlot
            {
                itemID = group.Key,
                Amount = group.Sum(i => i.Amount),
                itemCategory = group.First().itemCategory // 카테고리는 첫 번째 아이템 것을 그대로 사용
            };

            result.Add(stackedSlot);
        }

        return result;
    }

    // 포맷팅 결정 로직을 별도의 함수로 분리하여 가독성을 높였습니다.
    private (Func<InventorySlot, string> formatter, SlotSubTextStyle style) GetSlotStyleByCategory(ItemCategory category)
    {
        return category switch
        {
            ItemCategory.Equipment => (ItemSlotSubText.UpgradeLevel, SlotSubTextStyle.UpgradeLevel),
            ItemCategory.Material => (ItemSlotSubText.StackCount, SlotSubTextStyle.DefaultMaterial),
            ItemCategory.Consumable => (ItemSlotSubText.StackCount, SlotSubTextStyle.DefaultMaterial),
            _ => (ItemSlotSubText.None, SlotSubTextStyle.Default)
        };
    }

    public void Confirm()
    {
        Managers.Party.PlayerController.VictoryTime = false;
        Managers.Sound.StopAll();

        Managers.SceneEx.LoadScene(Define.Scene.Game);
    }

    // IItemSlotHandler 인터페이스의 필수 구현부 (클릭 시 툴팁을 띄우거나 무시)
    // 던전 결과창에서는 클릭 이벤트를 무시하거나 툴팁만 띄워주는 것이 일반적입니다.
    void OnSlotClicked(UI_ItemSlot slot) { }       // C# 8 default 구현
    void OnSlotDoubleClicked(UI_ItemSlot slot) { }  //  항상 void
    void OnSlotDrop(UI_ItemSlot from, UI_ItemSlot to) { }
    void OnSlotPointerEnter(UI_ItemSlot slot, Vector2 screenPos) { }
    void OnSlotPointerExit(UI_ItemSlot slot) { }
}
