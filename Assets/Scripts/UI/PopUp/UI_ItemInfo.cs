using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ItemInfo : UI_Base
{
    [SerializeField] Image _itemIcon;

    [SerializeField] TextMeshProUGUI _itemName;
    [SerializeField] TextMeshProUGUI _itemDescription;
    [SerializeField] TextMeshProUGUI _itemUpgradeLevel;

    [SerializeField] TextMeshProUGUI _itemGrade;
    [SerializeField] TextMeshProUGUI _itemTier;

    [SerializeField] Transform _mainStatParent;
    [SerializeField] Transform _subStatParent;

    private const int MAX_STAT_COUNT = 6;
    private List<UI_MainStatInfo> _mainStatSlots = new();
    private List<UI_SubStatInfo> _subStatSlots = new();

    //  Init에서 최초 1회 생성
    public override async void Init()
    {
        for (int i = 0; i < MAX_STAT_COUNT; i++)
        {
            var main = await Managers.UI.MakeSubItemAsync<UI_MainStatInfo>("UI_MainStatInfo", _mainStatParent);
            main.gameObject.SetActive(false);
            _mainStatSlots.Add(main);

            var sub = await Managers.UI.MakeSubItemAsync<UI_SubStatInfo>("UI_SubStatInfo", _subStatParent);
            sub.gameObject.SetActive(false);
            _subStatSlots.Add(sub);
        }
    }

    public void SetInfo(InventorySlot slotData)
    {
        if (slotData.IsEquipment) SetEquipmentInfo(slotData);
        else SetMaterialInfo(slotData);
    }

    public void SetEquipmentInfo(InventorySlot inventorySlot)
    {
        SetIcon(inventorySlot.itemID, ItemCategory.Equipment).Forget();
        SetItemName(inventorySlot.itemID, ItemCategory.Equipment);
        SetDescription(inventorySlot.itemID, ItemCategory.Equipment);
        SetUpgradeLevel(inventorySlot.EquipInstance.UpgradeLevel);
        SetGrade(inventorySlot.itemID, ItemCategory.Equipment);
        SetTier(inventorySlot.itemID, ItemCategory.Equipment);

        //  Destroy/생성 없이 SetActive만
        SetMainStat(inventorySlot.EquipInstance);
        SetSubStat(inventorySlot.EquipInstance);
    }

    public void SetMaterialInfo(InventorySlot inventorySlot)
    {
        SetIcon(inventorySlot.itemID, ItemCategory.Material).Forget();
        SetItemName(inventorySlot.itemID, ItemCategory.Material);
        SetDescription(inventorySlot.itemID, ItemCategory.Material);
        SetGrade(inventorySlot.itemID, ItemCategory.Material);
        SetTier(inventorySlot.itemID, ItemCategory.Material);
        _itemUpgradeLevel.text = "";

        //  재료 아이템은 스탯 없으므로 전부 숨기기
        _mainStatSlots.ForEach(s => s.gameObject.SetActive(false));
        _subStatSlots.ForEach(s => s.gameObject.SetActive(false));
    }

    //  완전 동기 - Destroy/생성 없음
    private void SetMainStat(EquipmentInstance instance)
    {
        var stats = instance.MainStats;
        for (int i = 0; i < _mainStatSlots.Count; i++)
        {
            bool active = i < stats.Count;
            _mainStatSlots[i].gameObject.SetActive(active);
            if (active) _mainStatSlots[i].SetInfo(stats[i]);
        }
    }

    private void SetSubStat(EquipmentInstance instance)
    {
        var stats = instance.SubStats;
        for (int i = 0; i < _subStatSlots.Count; i++)
        {
            bool active = i < stats.Count;
            _subStatSlots[i].gameObject.SetActive(active);
            if (active) _subStatSlots[i].SetInfo(stats[i]);
        }
    }

    public async UniTask SetIcon(int itemID, ItemCategory category)
    {
        Sprite icon = await Managers.Resource.LoadAsync<Sprite>(
            Managers.Data.GetItemData(itemID, category).IconKey, isGlobal: true);

        if (this == null || !gameObject.activeInHierarchy) return;
        _itemIcon.sprite = icon;
    }

    public void SetItemName(int itemID, ItemCategory category)
        => _itemName.text = Managers.Data.GetItemData(itemID, category).Name;

    public void SetDescription(int itemID, ItemCategory category)
        => _itemDescription.text = Managers.Data.GetItemData(itemID, category).Description;

    public void SetUpgradeLevel(int upgradeLevel)
        => _itemUpgradeLevel.text = "+" + upgradeLevel;

    public void SetGrade(int itemID, ItemCategory category)
    {
        ItemGrade grade = Managers.Data.GetItemData(itemID, category).Grade;
        _itemGrade.text = grade.ToString();
        _itemGrade.color = ColorDict.GetGradeColor(grade);
    }

    public void SetTier(int itemID, ItemCategory category)
    {
        BaseItemData data = Managers.Data.GetItemData(itemID, category);
        int tier = data is EquipmentData eq ? eq.Tier
                 : data is MaterialData mat ? mat.Tier : 0;
        _itemTier.text = $"Tier {tier}";
    }


}
