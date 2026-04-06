using NPOI.SS.Formula.PTG;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquipType
{
    Badge = 0,
    Bag,
    Charm,
    Gloves,
    Hairpin,
    Hat,
    Necklace,
    Shoes,
    Watch
}


public class EquipmentManager
{
    public Dictionary<EquipType, InventorySlot> _equippedItem = new Dictionary<EquipType, InventorySlot>();

    // 장비 변경 이벤트 (부위, 새로 장착된 아이템, 벗은 아이템)
    public event Action<EquipType, InventorySlot, InventorySlot> OnEquipmentChanged;
    public event Action<ItemCategory> OnInventoryChanged;

    public void Init()
    {
        for (int i = 0; i < 9; i++)
        {
            _equippedItem[(EquipType)i] = null ;
        }
    }

    // 인벤토리 인덱스를 통해 장착
    public void Equip(int inventoryIndex)
    {
        // 이 함수는 인벤토리의 InventorySlot UI_ItemSlot에서 호출될거임

        //아이템 장착의 흐름

        // 인벤토리 장비탭에서 장비를 DoubleClick or Drage 함
        // 더블 클릭의 경우 : EquipmentManager에서 해당 아이템의 슬롯과 교체 혹은 장착이 이루어짐
        //                  : 이미 장비가 장착 되어 있는경우 -> 2개의 슬롯을 교체 -> 인벤토리는 해당슬롯만 갱신하면됨
        //                  : 장비 슬롯이 비어있었을 경우    -> 장비 장착 -> 인벤토리는 해당슬롯만 비우면됨


        //장비 인벤에서 해당 슬롯을 가져옴
        InventorySlot invenSlot = Managers.Inventory.Inventory[ItemCategory.Equipment][inventoryIndex];
        if (invenSlot == null || invenSlot.IsEmpty) return;

        // DataManager에서 EquipmentData 가져오기
        EquipmentData itemData = Managers.Data.GetData<int, EquipmentData>(invenSlot.itemID);
        if (itemData == null) return;

        //문자열 EquipPart를 EquipType Enum으로 안전하게 변환
        //장비 타임이 뭔지 확인
        if (!Enum.TryParse(itemData.EquipPart, true, out EquipType type))
        {
            Debug.LogError($"장비 부위 변환 실패: {itemData.EquipPart}");
            return;
        }

        InventorySlot newEquip = new InventorySlot
        {
            itemID = invenSlot.itemID,
            Amount = 1,
            EquipInstance = invenSlot.EquipInstance // 이 줄 추가!
        };
        invenSlot.Clear();

        // 그 다음 기존 장비를 벗깁니다. (확보된 빈 공간으로 안전하게 들어감)
        if (_equippedItem.ContainsKey(type) && _equippedItem[type] != null && !_equippedItem[type].IsEmpty)
        {
            UnEquip(type);
        }

        // 3. 새 장비 장착 적용
        _equippedItem[type] = newEquip;


        // [핵심 변경점] 현재 파티원 4명 모두에게 스탯을 적용합니다.
        List<BaseCharacter> partyMembers = Managers.Party.GetMember();
        foreach (BaseCharacter member in partyMembers)
        {
            if (member != null && member.Stat != null)
            {
                ApplyEquipment(member.Stat, newEquip);

                member.Stat.RefreshStatsUI();
            }
        }

        // 4. UI 갱신 이벤트 호출
        OnInventoryChanged?.Invoke(ItemCategory.Equipment);
        OnEquipmentChanged?.Invoke(type, newEquip, null);
    }

    // 장비 부위를 받아 장착 해제
    public void UnEquip(EquipType type)
    {
        // 1, 2, 3 방어코드를 하나로 깔끔하게 합칩니다.
        if (!_equippedItem.ContainsKey(type) || _equippedItem[type] == null || _equippedItem[type].IsEmpty)
            return;

        InventorySlot unequippedItem = _equippedItem[type];

        // [핵심 변경점] 현재 파티원 4명 모두에게서 스탯을 제거합니다.
        List<BaseCharacter> partyMembers = Managers.Party.GetMember();
        foreach (BaseCharacter member in partyMembers)
        {
            if (member != null && member.Stat != null)
            {
                ReleaseEquipment(member.Stat, unequippedItem);
                member.Stat.RefreshStatsUI();
            }
        }

        // 1. 벗은 장비를 인벤토리에 다시 추가
        Managers.Inventory.AddEquipmentSlot(unequippedItem);

        // 2. 장착 슬롯 비우기
        _equippedItem[type] = null;

        //UI 갱신 및 스탯 재계산 이벤트 호출
        OnEquipmentChanged?.Invoke(type, null, unequippedItem);
    }

    private void ApplyEquipment(CharacterStat playerStat, InventorySlot equipment)
    {
        //메인스탯
        List<StatOption> mainStats = equipment.EquipInstance.MainStats;
        foreach (StatOption statOption in mainStats)
        {
            Stat targetStat = playerStat.GetStat(statOption.StatType);

            StatModType modType = statOption.StatType.ToString().Contains("Percent")
                ? StatModType.PercentAdd
                : StatModType.Flat;

            // 3. Modifier 추가 (이제 Stat 클래스가 알아서 (Base * Percent) + Flat 공식으로 계산함)
            targetStat.AddModifier(new StatModifier(statOption.Value, modType, equipment));
        }

        //서브스탯
        List<StatOption> subStats = equipment.EquipInstance.SubStats;
        foreach (StatOption statOption in subStats)
        {
            Stat targetStat = playerStat.GetStat(statOption.StatType);

            StatModType modType = statOption.StatType.ToString().Contains("Percent")
                ? StatModType.PercentAdd
                : StatModType.Flat;

            // 3. Modifier 추가 (이제 Stat 클래스가 알아서 (Base * Percent) + Flat 공식으로 계산함)
            targetStat.AddModifier(new StatModifier(statOption.Value, modType, equipment));
        }
    }

    public void ReleaseEquipment(CharacterStat playerStat, InventorySlot equipment)
    {
        ////메인스탯
        //List<StatOption> mainStats = equipment.EquipInstance.MainStats;
        //foreach (StatOption statOption in mainStats)
        //{
        //    Stat targetStat = playerStat.GetStat(statOption.StatType);

        //    StatModType modType = statOption.StatType.ToString().Contains("Percent")
        //        ? StatModType.PercentAdd
        //        : StatModType.Flat;

        //    // 3. Modifier 추가 (이제 Stat 클래스가 알아서 (Base * Percent) + Flat 공식으로 계산함)
        //    targetStat.RemoveAllModifiersFromSource(equipment);
        //}

        ////서브스탯
        //List<StatOption> subStats = equipment.EquipInstance.SubStats;
        //foreach (StatOption statOption in subStats)
        //{
        //    Stat targetStat = playerStat.GetStat(statOption.StatType);

        //    StatModType modType = statOption.StatType.ToString().Contains("Percent")
        //        ? StatModType.PercentAdd
        //        : StatModType.Flat;

        //    // 3. Modifier 추가 (이제 Stat 클래스가 알아서 (Base * Percent) + Flat 공식으로 계산함)
        //    targetStat.RemoveAllModifiersFromSource(equipment);
        //}

        //메인스탯
        List<StatOption> mainStats = equipment.EquipInstance.MainStats;    
        foreach (StatOption statOption in mainStats)
            playerStat.GetStat(statOption.StatType)?.RemoveAllModifiersFromSource(equipment);

        //서브스탯
        List<StatOption> subStats = equipment.EquipInstance.SubStats;
        foreach (StatOption statOption in subStats)
            playerStat.GetStat(statOption.StatType)?.RemoveAllModifiersFromSource(equipment);
    }

    public EquipmentSaveData GetSaveData()
    {
        EquipmentSaveData save = new EquipmentSaveData();

        foreach (KeyValuePair<EquipType, InventorySlot> entry in _equippedItem)
        {
            // null이거나 비어있는 슬롯은 저장 제외
            if (entry.Value == null || entry.Value.IsEmpty) continue;

            save.equippedSlots.Add(new EquippedSlotEntry
            {
                slotType = entry.Key,
                slot = entry.Value
            });
        }

        return save;
    }
    
    public void LoadSaveData(EquipmentSaveData save)
    {
        if (save?.equippedSlots == null) return;

        foreach (var entry in save.equippedSlots)
        {
            if (entry.slot == null || entry.slot.IsEmpty) continue;

            // 장착 슬롯 복원
            _equippedItem[entry.slotType] = entry.slot;

            // 파티원 전체에 스탯 재적용
            foreach (var member in Managers.Party.GetMember())
            {
                if (member?.Stat != null)
                    ApplyEquipment(member.Stat, entry.slot);
            }
        }

        // UI 갱신
        foreach (EquipType type in Enum.GetValues(typeof(EquipType)))
        {
            InventorySlot slot = _equippedItem.ContainsKey(type) ? _equippedItem[type] : null;
            OnEquipmentChanged?.Invoke(type, slot, null);
        }
    }
}
