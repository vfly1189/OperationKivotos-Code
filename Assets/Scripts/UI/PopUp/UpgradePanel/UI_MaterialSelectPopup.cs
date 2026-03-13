using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public enum UpgradeBookID
{
    Small = 30003,
    Medium,
    Large
}


public class UI_MaterialSelectPopup : UI_PopUp
{
    [Header("µî·Ï & Ãë¼Ò ¹öÆ°")]
    [SerializeField] Button _confirmButton;
    [SerializeField] Button _cancelButton;

    [Header("ÄÁÅÙÃ÷ ºä")]
    [SerializeField] Transform _contents;

    private List<UI_ItemSlot> _uiSlots = new List<UI_ItemSlot>();
    private InventorySlot _selectedSlot = null;

    public override void Init()
    {
        SetContentView();

        _cancelButton.onClick.AddListener(ClosePopupUI);
    }

    private async void SetContentView()
    {
        InventorySlot[] materialSlots = Managers.Inventory.Inventory[ItemCategory.Material];

        foreach (InventorySlot slot in materialSlots)
        {
            if (slot.IsEmpty) continue;


            if(slot.itemID != (int)UpgradeBookID.Small && slot.itemID != (int)UpgradeBookID.Medium 
                && slot.itemID != (int)UpgradeBookID.Large)
                { continue; }

            UI_ItemSlot uiSlot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _contents);

            uiSlot.gameObject.SetActive(true);
            uiSlot.SetInfo(slot, ItemCategory.Material, -1);
            uiSlot.SetCallback(null, null);
            _uiSlots.Add(uiSlot);
        }

        InventorySlot[] equipmentSlots = Managers.Inventory.Inventory[ItemCategory.Equipment];

        foreach (InventorySlot slot in equipmentSlots)
        {
            if (slot.IsEmpty || (_selectedSlot != null && _selectedSlot == slot)) continue;

            UI_ItemSlot uiSlot = await Managers.UI.MakeSubItemAsync<UI_ItemSlot>("UI_ItemSlot", _contents);

            uiSlot.gameObject.SetActive(true);
            uiSlot.SetInfo(slot, ItemCategory.Equipment, -1);
            uiSlot.SetCallback(null, null);
            _uiSlots.Add(uiSlot);
        }
    }

    public void SetSelectedSlot(InventorySlot slot) { _selectedSlot = slot; }

    private void OnDestroy()
    {
        foreach (UI_ItemSlot slot in _uiSlots)
        {
            slot.SetCallback(null, null);
            Managers.Resource.Destroy(slot.gameObject);
        }
    }
}
