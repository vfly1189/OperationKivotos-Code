using UnityEngine;

public class MaterialEntry
{
    public InventorySlot Slot { get; set; }
    public int Count { get; set; }

    public bool IsEmpty => Slot == null || Count <= 0;
    public bool IsBook => Slot != null && IsUpgradeBook(Slot.itemID);

    public void Clear() { Slot = null; Count = 0; }

    public static bool IsUpgradeBook(int itemID) =>
        itemID == (int)UpgradeBookID.Small ||
        itemID == (int)UpgradeBookID.Medium ||
        itemID == (int)UpgradeBookID.Large;
}