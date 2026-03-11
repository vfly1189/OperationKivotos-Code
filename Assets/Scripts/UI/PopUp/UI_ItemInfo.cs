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
    }

    public void SetEquipmentInfo(InventorySlot inventorySlot)
    {
        SetIcon(inventorySlot.itemID).Forget();
        SetMainStat(inventorySlot.EquipInstance).Forget();
        SetSubStat(inventorySlot.EquipInstance).Forget();

        SetItemName(inventorySlot.itemID);
        SetDescription(inventorySlot.itemID);
        SetUpgradeLevel(inventorySlot.EquipInstance.UpgradeLevel);
        SetGrade(inventorySlot.itemID);
        SetTier(inventorySlot.itemID);
    }

    public async UniTask SetIcon(int itemID)
    {
        Sprite icon = await Managers.Resource.LoadAsync<Sprite>(Managers.Data.GetItemData(itemID, ItemCategory.Equipment).IconKey);
        
        if (this == null || gameObject == null || !gameObject.activeInHierarchy)
            return;

        _itemIcon.sprite = icon;
    }

    public void SetItemName(int itemID)
    {
        _itemName.text = Managers.Data.GetItemData(itemID, ItemCategory.Equipment).Name;
    }

    public void SetDescription(int itemID)
    {
        _itemDescription.text = Managers.Data.GetItemData(itemID, ItemCategory.Equipment).Description;
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
    
    public void SetGrade(int itemID)
    {
        ItemGrade grade = Managers.Data.GetItemData(itemID, ItemCategory.Equipment).Grade;

        _itemGrade.text = grade.ToString();
        _itemGrade.color = ColorDict.GetGradeColor(grade);
    }
    
    public void SetTier(int itemID)
    {
        BaseItemData data = Managers.Data.GetItemData(itemID, ItemCategory.Equipment);
        
        EquipmentData equipData = data as EquipmentData;

        int tier = equipData.Tier;

        _itemTier.text = $"Tier {tier}";
    }

    
}
