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

    public override void Init()
    {
        
    }

    public void SetInfo(InventorySlot slotData)
    {
        if(slotData.IsEquipment)
            SetEquipmentInfo(slotData);
        else
            SetMaterialInfo(slotData);
    }

    public void SetEquipmentInfo(InventorySlot inventorySlot)
    {
        SetIcon(inventorySlot.itemID, ItemCategory.Equipment).Forget();
        SetMainStat(inventorySlot.EquipInstance).Forget();
        SetSubStat(inventorySlot.EquipInstance).Forget();

        SetItemName(inventorySlot.itemID, ItemCategory.Equipment);
        SetDescription(inventorySlot.itemID, ItemCategory.Equipment);
        SetUpgradeLevel(inventorySlot.EquipInstance.UpgradeLevel);
        SetGrade(inventorySlot.itemID, ItemCategory.Equipment);
        SetTier(inventorySlot.itemID, ItemCategory.Equipment);
    }

    public void SetMaterialInfo(InventorySlot inventorySlot)
    {
        SetIcon(inventorySlot.itemID, ItemCategory.Material).Forget();

        SetItemName(inventorySlot.itemID, ItemCategory.Material);
        SetDescription(inventorySlot.itemID, ItemCategory.Material);
        SetGrade(inventorySlot.itemID, ItemCategory.Material);
        SetTier(inventorySlot.itemID, ItemCategory.Material);


        _itemUpgradeLevel.text = "";
        foreach (Transform child in _subStatParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        foreach (Transform child in _mainStatParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }
    }

    public async UniTask SetIcon(int itemID, ItemCategory category)
    {
        Sprite icon = await Managers.Resource.LoadAsync<Sprite>(Managers.Data.GetItemData(itemID, category).IconKey);
        
        if (this == null || gameObject == null || !gameObject.activeInHierarchy)
            return;

        _itemIcon.sprite = icon;
    }

    public void SetItemName(int itemID, ItemCategory category)
    {
        _itemName.text = Managers.Data.GetItemData(itemID, category).Name;
    }

    public void SetDescription(int itemID, ItemCategory category)
    {
        _itemDescription.text = Managers.Data.GetItemData(itemID, category).Description;
    }

    public void SetUpgradeLevel(int upgradeLevel)
    {
        _itemUpgradeLevel.text = "+" + upgradeLevel.ToString();
    }

    public async UniTask SetMainStat(EquipmentInstance equipmentInstance)
    {
        foreach (Transform child in _mainStatParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        List<StatOption> mainStats = equipmentInstance.MainStats;

        foreach (StatOption statOption in mainStats)
        {
            UI_MainStatInfo mainStatInfo = await Managers.UI.MakeSubItemAsync<UI_MainStatInfo>("UI_MainStatInfo", _mainStatParent);

            if (this == null || gameObject == null || !gameObject.activeInHierarchy)
                return;

            if (mainStatInfo != null)
            {
                mainStatInfo.SetInfo(statOption);
            }
        }
    }

    public async UniTask SetSubStat(EquipmentInstance equipmentInstance)
    {
        foreach (Transform child in _subStatParent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        List<StatOption> subStats = equipmentInstance.SubStats;

        foreach (StatOption statOption in subStats)
        {
            UI_SubStatInfo subStatInfo = await Managers.UI.MakeSubItemAsync<UI_SubStatInfo>("UI_SubStatInfo", _subStatParent);
            
            if (this == null || gameObject == null || !gameObject.activeInHierarchy)
                return;

            if (subStatInfo != null)
            {
                subStatInfo.SetInfo(statOption);
            }
        }
    }
    
    public void SetGrade(int itemID, ItemCategory category)
    {
        ItemGrade grade = Managers.Data.GetItemData(itemID, category).Grade;

        _itemGrade.text = grade.ToString();
        _itemGrade.color = ColorDict.GetGradeColor(grade);
    }
    
    public void SetTier(int itemID, ItemCategory category)
    {
        BaseItemData data = Managers.Data.GetItemData(itemID, category);

        int tier = 0;
        if (category == ItemCategory.Equipment)
        {
            EquipmentData equipData = data as EquipmentData;
            tier = equipData.Tier;
        }
        else if(category == ItemCategory.Material)
        {
            MaterialData materialData = data as MaterialData;
            tier = materialData.Tier;
        }

        _itemTier.text = $"Tier {tier}";
    }

    
}
