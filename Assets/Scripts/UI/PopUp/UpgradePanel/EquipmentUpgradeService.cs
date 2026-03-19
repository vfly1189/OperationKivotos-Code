using Cysharp.Threading.Tasks.Triggers;
using NPOI.OpenXmlFormats.Dml;
using NPOI.Util;
using Org.BouncyCastle.Bcpg;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WeaponUpgradeService;

public readonly struct UpgradeResult
{
    public readonly int PrevLevel;
    public readonly int NewLevel;

    public UpgradeResult(int prev, int next)
    {
        PrevLevel = prev;
        NewLevel = next;
    }
}

public class EquipmentUpgradeService
{
    //강화 재료 등록하는 슬롯의 최대 갯수
    public const int MAX_MATERIAL_SLOTS = 8;

    // UI가 구독할 이벤트
    public event Action<InventorySlot> OnEquipmentSelected;
    public event Action<int, MaterialEntry> OnMaterialSlotChanged; // (슬롯인덱스, 데이터)
    
    public InventorySlot SelectedEquipment { get; private set; }

    //InventorySlot으로 강화책인지 장비인지 판별하게 귀찮아서 새로팜
    private readonly MaterialEntry[] _materialSlots;
    public IReadOnlyList<MaterialEntry> MaterialSlots => _materialSlots;

    private readonly int[] _requireAccumulatedCost = new int[16];

    public event Action<ExpPreviewResult> OnExpPreviewChanged;
    public event Action<InventorySlot, UpgradeResult> OnUpgradeExecuted;
    //생성자
    public EquipmentUpgradeService()
    {
        _materialSlots = new MaterialEntry[MAX_MATERIAL_SLOTS];
        for (int i = 0; i < MAX_MATERIAL_SLOTS; i++)
            _materialSlots[i] = new MaterialEntry();

        for(int i=1; i<=15; i++)
        {
            _requireAccumulatedCost[i] = _requireAccumulatedCost[i - 1] + Managers.Data.GetData<int, EquipmentUpgradeCost>(i).EnhancementCost;
        }
    }

    public int CalcCredit(int curLevel, int targetLevel)
    {
        return _requireAccumulatedCost[targetLevel] - _requireAccumulatedCost[curLevel];
    }

    // =========================================================
    // 장비 선택
    // =========================================================
    public void SelectEquipment(InventorySlot slot)
    {
        SelectedEquipment = slot;
        ClearMaterials(); // 장비 교체 시 재료 초기화
        OnEquipmentSelected?.Invoke(slot);
    }

    // =========================================================
    // 재료 관리
    // =========================================================
    public bool TryAddMaterial(InventorySlot slot)
    {
        if (!CanAddMoreMaterial()) return false;


        if (MaterialEntry.IsUpgradeBook(slot.itemID))
        {
            // 같은 책 타입 이미 있으면 카운트++
            for (int i = 0; i < MAX_MATERIAL_SLOTS; i++)
            {
                if (!_materialSlots[i].IsEmpty && _materialSlots[i].Slot.itemID == slot.itemID)
                {
                    if (_materialSlots[i].Count >= slot.Amount) return false; // 인벤 수량 초과
                    _materialSlots[i].Count++;
                    OnMaterialSlotChanged?.Invoke(i, _materialSlots[i]);
                    NotifyExpPreview();
                    return true;
                }
            }
            // 같은 책 없으면 새 슬롯에 등록
        }
        else
        {
            //  장비는 이미 등록된 것이면 중복 차단
            for (int i = 0; i < MAX_MATERIAL_SLOTS; i++)
                if (!_materialSlots[i].IsEmpty && _materialSlots[i].Slot == slot) return false;
        }

        // 빈 슬롯에 신규 등록
        for (int i = 0; i < MAX_MATERIAL_SLOTS; i++)
        {
            if (_materialSlots[i].IsEmpty)
            {
                _materialSlots[i].Slot = slot;
                _materialSlots[i].Count = 1;
                OnMaterialSlotChanged?.Invoke(i, _materialSlots[i]);
                NotifyExpPreview();
                return true;
            }
        }
        return false; // 슬롯 가득 참
    }

    // - 버튼을 눌러서 재료 등록을 해제
    public void RemoveMaterial(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_MATERIAL_SLOTS) return;
        var entry = _materialSlots[slotIndex];
        if (entry.IsEmpty) return;

        //  책은 1개씩 감소, 장비 or 책 마지막 1개는 슬롯 비움
        if (entry.IsBook && entry.Count > 1)
        {
            entry.Count--;
            OnMaterialSlotChanged?.Invoke(slotIndex, entry);
        }
        else
        {
            entry.Clear();
            OnMaterialSlotChanged?.Invoke(slotIndex, null);
        }
        NotifyExpPreview();
    }

    // 팝업에서 취소 버튼 연결용 - slot으로 인덱스 역조회
    public int FindMaterialSlotIndex(InventorySlot slot)
    {
        for (int i = 0; i < MAX_MATERIAL_SLOTS; i++)
            if (!_materialSlots[i].IsEmpty && _materialSlots[i].Slot == slot) return i;
        return -1;
    }

    private bool CanAddMoreMaterial()
    {
        if (SelectedEquipment != null)
        {
            int itemID = SelectedEquipment.itemID;

            ItemGrade grade = Managers.Data.GetItemData(itemID, ItemCategory.Equipment).Grade;

            return CalculatePreviewExp().SimulatedLevel < Managers.Data.GetData<ItemGrade, GradeConfig>(grade).MaxLevel;
        }
        else
            return false;
    }


    public int GetSelectedCount(InventorySlot slot)
    {
        int count = 0;
        foreach (MaterialEntry entry in _materialSlots) // 내부 재료 리스트
        {
            if (entry != null && entry.Slot == slot)
                count+=entry.Count;
        }
        return count;
    }

    public void ClearMaterials()
    {
        for (int i = 0; i < MAX_MATERIAL_SLOTS; i++)
        {
            if (!_materialSlots[i].IsEmpty)
            {
                _materialSlots[i].Clear();
                OnMaterialSlotChanged?.Invoke(i, null);
            }
        }
    }

    public List<InventorySlot> GetAvailableMaterials()
    {
        var result = new List<InventorySlot>();

        foreach (var slot in Managers.Inventory.Inventory[ItemCategory.Material])
            if (!slot.IsEmpty && MaterialEntry.IsUpgradeBook(slot.itemID))
                result.Add(slot);

        foreach (var slot in Managers.Inventory.Inventory[ItemCategory.Equipment])
        {
            if (slot.IsEmpty || slot == SelectedEquipment) continue;
            result.Add(slot);
        }
        return result;
    }

    private void NotifyExpPreview()
    {
        OnExpPreviewChanged?.Invoke(CalculatePreviewExp());
    }

    public ExpPreviewResult CalculatePreviewExp()
    {
        if (SelectedEquipment == null)
            return ExpPreviewResult.Empty;

        int totalGainExp = 0;

        foreach (MaterialEntry entry in _materialSlots)
        {
            if (entry.IsEmpty) continue;

            int expPerItem = GetExpByMaterial(entry);
            totalGainExp += expPerItem * entry.Count;
        }

        var instance = SelectedEquipment.EquipInstance;
        int currentExp = instance.CurrentExp;
        int requireExp = instance.NextLevelRequireExp;
        int currentLevel = instance.UpgradeLevel;

        // 레벨업 시뮬레이션
        int simulatedExp = currentExp + totalGainExp;
        int simulatedLevel = currentLevel;
        int simulatedRequire = requireExp;

        while (simulatedExp >= simulatedRequire)
        {
            simulatedExp -= simulatedRequire;
            simulatedLevel++;

            int maxLevel = GetItemMaxLevel(SelectedEquipment.itemID);

            if (simulatedLevel >= maxLevel)
            {
                simulatedRequire = 0;
                break;
            }
            else 
            {
                simulatedRequire = Managers.Data.GetData<int, EquipmentLevelExpData>(simulatedLevel + 1).RequireExp; // 데이터 참조              
            }       
        }

        return new ExpPreviewResult(
            gainExp: totalGainExp,
            simulatedLevel: simulatedLevel,
            simulatedExp: simulatedExp,
            simulatedRequireExp: simulatedRequire
        );
    }

    private int GetExpByMaterial(MaterialEntry entry)
    {
        if (entry.IsBook)
            return Managers.Data.GetData<int, EquipmentUpgradeBookExpData>(entry.Slot.itemID).ExpValue;
        else
        {
            return GetEquipmentDecompositionExp(entry.Slot) + entry.Slot.EquipInstance.CurrentExp;
        }
    }

    private int GetEquipmentDecompositionExp(InventorySlot slot)
    {
        // 장비 등급/레벨에 따라 제공 경험치 계산
        EquipmentData data = Managers.Data.GetData<int, EquipmentData>(slot.itemID);

        EquipmentDecompositionData decomposeData = Managers.Data.GetData<int, EquipmentDecompositionData>(data.Tier);
        int mat1Exp = Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Small).ExpValue;
        int mat2Exp = Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Medium).ExpValue;
        int mat3Exp = Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Large).ExpValue;
        return decomposeData.Mat1_Count * mat1Exp + decomposeData.Mat2_Count * mat2Exp + decomposeData.Mat3_Count * mat3Exp;
    }

    public bool ExecuteUpgrade()
    {
        if (!CanUpgrade()) return false;


        ExpPreviewResult preview = CalculatePreviewExp();
        EquipmentInstance instance = SelectedEquipment.EquipInstance;

        int currentLevel = instance.UpgradeLevel;
        int targetLevel = preview.SimulatedLevel;

        int creditCost = CalcCredit(currentLevel, targetLevel);
        if (!Managers.Wallet.ConsumeCurrency(CurrencyType.Credit, creditCost)) return false;

        if (targetLevel == GetItemMaxLevel(SelectedEquipment.itemID)) 
        {
            //남은게
            int rest = preview.SimulatedExp;

            List<int> upgradeBookExp = new List<int>();
            upgradeBookExp.Add(Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Large).ExpValue);
            upgradeBookExp.Add(Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Medium).ExpValue);
            upgradeBookExp.Add(Managers.Data.GetData<int, EquipmentUpgradeBookExpData>((int)UpgradeBookID.Small).ExpValue);

            List<int> restUpgradeBookCount = new List<int>();
            foreach(int value in upgradeBookExp)
            {
                restUpgradeBookCount.Add(rest / value);
                rest = rest % value;
            }
         
            Managers.Inventory.AddItem((int)UpgradeBookID.Large, ItemCategory.Material, restUpgradeBookCount[0]);
            Managers.Inventory.AddItem((int)UpgradeBookID.Medium, ItemCategory.Material, restUpgradeBookCount[1]); 
            Managers.Inventory.AddItem((int)UpgradeBookID.Small, ItemCategory.Material, restUpgradeBookCount[2]);
        }


     
        for (int lv = currentLevel + 1; lv <= targetLevel; lv++)
        {
            GrowMainStat(instance);

            if (lv % 3 == 0 && instance.SubStats.Count > 0)
                GrowRandomSubStat(instance);
        }


        instance.UpgradeLevel = preview.SimulatedLevel;
        instance.CurrentExp = preview.SimulatedExp;
        instance.NextLevelRequireExp = preview.SimulatedRequireExp;

        ConsumeUsedMaterials();
        ClearMaterials();

        OnUpgradeExecuted?.Invoke(SelectedEquipment, new UpgradeResult(currentLevel, targetLevel));
        return true;
    }

    // EquipmentUpgradeService
    public void DeselectEquipment()
    {
        SelectedEquipment = null;
        ClearMaterials();
    }

    private void ConsumeUsedMaterials()
    {

        foreach (MaterialEntry entry in _materialSlots)
        {
            if (entry.IsEmpty) continue;

            if (entry.IsBook)
                Managers.Inventory.ConsumeMaterial(entry.Slot.itemID, entry.Count);
            else
                Managers.Inventory.RemoveSlot(entry.Slot); // ← 슬롯 참조 기반으로 교체
        }
    }

    private void GrowMainStat(EquipmentInstance instance)
    {
        if (instance.MainStats.Count == 0) return;

        var mainStat = instance.MainStats[0];
        EquipmentData equipData = Managers.Data.GetData<int, EquipmentData>(SelectedEquipment.itemID);
        StatPoolData pool = Managers.Data.GetData<int, StatPoolData>(equipData.MainStatPoolID);
        StatPoolEntry entry = pool?.Entries.Find(e => e.StatType == mainStat.StatType);

        // MainStat은 Min = Max = 고정값이므로 MinValue 그대로 사용
        if (entry != null)
            mainStat.Value += entry.UpgradeMinValue;
    }

    private void GrowRandomSubStat(EquipmentInstance instance)
    {
        int idx = UnityEngine.Random.Range(0, instance.SubStats.Count);
        StatOption target = instance.SubStats[idx];

        EquipmentData equipData = Managers.Data.GetData<int, EquipmentData>(SelectedEquipment.itemID);
        StatPoolData pool = Managers.Data.GetData<int, StatPoolData>(equipData.SubStatPoolID);
        StatPoolEntry entry = pool?.Entries.Find(e => e.StatType == target.StatType);

        if (entry == null) return;

        List<float> values = new List<float>();
        values.Add(entry.UpgradeMinValue);
        values.Add(entry.UpgradeMaxValue);
        values.Add((entry.UpgradeMinValue + entry.UpgradeMaxValue) / 2f);

        int valueIdx = UnityEngine.Random.Range(0, values.Count);


        target.Value += values[valueIdx];
        target.UpgradeCount++;
        
    }

    public int GetItemMaxLevel(int itemID)
    {
        ItemGrade grade = Managers.Data.GetItemData(itemID, ItemCategory.Equipment).Grade;
        int maxLevel = Managers.Data.GetData<ItemGrade, GradeConfig>(grade).MaxLevel;

        return maxLevel;
    }

    public bool CanUpgrade()
        => SelectedEquipment != null && _materialSlots.Any(s => !s.IsEmpty);


}
