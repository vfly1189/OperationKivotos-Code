// EquipmentDecomposeService.cs
using System;
using System.Collections.Generic;

public class EquipmentDecomposeService
{
    public const int MAX_DECOMPOSE_COUNT = 24;

    private readonly MaterialEntry[] _materialSlots;
    public IReadOnlyList<MaterialEntry> MaterialSlots => _materialSlots;

    public event Action<int, MaterialEntry> OnMaterialSlotChanged;
    public event Action OnDecomposeExecuted;
    public event Action OnMaterialsCleared;


    private int[] _upgradeBookResultCounts = new int[3];

    public EquipmentDecomposeService()
    {
        _materialSlots = new MaterialEntry[MAX_DECOMPOSE_COUNT];
        for (int i = 0; i < MAX_DECOMPOSE_COUNT; i++)
            _materialSlots[i] = new MaterialEntry();
    }

    public void SelectEquipment(InventorySlot slot) => TryAddMaterial(slot);

    public bool TryAddMaterial(InventorySlot slot)
    {
        if (!CanAddMoreMaterial()) return false;

        for (int i = 0; i < MAX_DECOMPOSE_COUNT; i++)
            if (!_materialSlots[i].IsEmpty && _materialSlots[i].Slot == slot) return false;

        for (int i = 0; i < MAX_DECOMPOSE_COUNT; i++)
        {
            if (!_materialSlots[i].IsEmpty) continue;

            _materialSlots[i].Slot = slot;
            _materialSlots[i].Count = 1;
            EvaluateResult(true, slot);
            OnMaterialSlotChanged?.Invoke(i, _materialSlots[i]);
            return true;
        }

        return false;
    }

    public void RemoveMaterial(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_DECOMPOSE_COUNT) return;

        MaterialEntry entry = _materialSlots[slotIndex];
        if (entry.IsEmpty) return;

        EvaluateResult(false, entry.Slot);
        entry.Clear();
        OnMaterialSlotChanged?.Invoke(slotIndex, null);
    }

    public void ClearMaterials()
    {
        for (int i = 0; i < MAX_DECOMPOSE_COUNT; i++)
            _materialSlots[i].Clear();

        Array.Clear(_upgradeBookResultCounts, 0, _upgradeBookResultCounts.Length); 

        OnMaterialsCleared?.Invoke(); // 1번만 발화
    }

    public void ExecuteDecompose()
    {
        Managers.Inventory.AddItem((int)UpgradeBookID.Small, ItemCategory.Material, _upgradeBookResultCounts[0]);
        Managers.Inventory.AddItem((int)UpgradeBookID.Medium, ItemCategory.Material, _upgradeBookResultCounts[1]);
        Managers.Inventory.AddItem((int)UpgradeBookID.Large, ItemCategory.Material, _upgradeBookResultCounts[2]);

        ConsumeUsedMaterials();

        //_upgradeBookResultCounts = new int[3];

        ClearMaterials();

        OnDecomposeExecuted?.Invoke();
    }

    public IReadOnlyList<int> GetResult() => _upgradeBookResultCounts;

    public int FindMaterialSlotIndex(InventorySlot slot)
    {
        for (int i = 0; i < MAX_DECOMPOSE_COUNT; i++)
            if (!_materialSlots[i].IsEmpty && _materialSlots[i].Slot == slot) return i;
        return -1;
    }

    // -----------------------------------------------------------------------

    private void EvaluateResult(bool isAdd, InventorySlot slot)
    {
        var data = Managers.Data.GetItemData(slot.itemID, ItemCategory.Equipment) as EquipmentData;
        var decomposeData = Managers.Data.GetData<int, EquipmentDecompositionData>(data.Tier);

        int sign = isAdd ? 1 : -1;

        _upgradeBookResultCounts[0] += sign * decomposeData.Mat1_Count;
        _upgradeBookResultCounts[1] += sign * decomposeData.Mat2_Count;
        _upgradeBookResultCounts[2] += sign * decomposeData.Mat3_Count;

        int equipExp = slot.EquipInstance?.CurrentExp ?? 0;
        if (equipExp <= 0) return;

        int[] bookExp =
        {
            Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Small).ExpValue,
            Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Medium).ExpValue,
            Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Large).ExpValue,
        };

        for (int i = 2; i >= 0; i--)
        {
            _upgradeBookResultCounts[i] += sign * (equipExp / bookExp[i]);
            equipExp %= bookExp[i];
        }
    }

    private void ConsumeUsedMaterials()
    {
        foreach (MaterialEntry entry in _materialSlots)
        {
            if (entry.IsEmpty) continue;
            Managers.Inventory.RemoveSlot(entry.Slot);
        }
    }

  

    private bool CanAddMoreMaterial()
    {
        for (int i = 0; i < MAX_DECOMPOSE_COUNT; i++)
            if (_materialSlots[i].IsEmpty) return true;
        return false;
    }
}
