using System.Collections.Generic;
using UnityEngine;

public static class EquipmentFactory
{
    // 랜덤 스탯이 부여된 완성된 InventorySlot 객체를 반환
    public static InventorySlot CreateEquipment(int itemID)
    {
        EquipmentData data = Managers.Data.GetData<int, EquipmentData>(itemID);
        if (data == null) return null;

        InventorySlot newSlot = new InventorySlot();
        newSlot.itemID = itemID;
        newSlot.Amount = 1;
        newSlot.EquipInstance = new EquipmentInstance();

        ApplyMainStat(data, newSlot.EquipInstance);
        ApplySubStats(data, newSlot.EquipInstance);

        return newSlot;
    }

    private static void ApplyMainStat(EquipmentData data, EquipmentInstance instance)
    {
        StatPoolData mainPool = Managers.Data.GetData<int, StatPoolData>(data.MainStatPoolID);
        if (mainPool == null || mainPool.Entries.Count == 0) return;

        StatPoolEntry pickedEntry = PickRandomEntryByWeight(mainPool.Entries);
        if (pickedEntry != null)
        {
            instance.MainStats.Add(new StatOption()
            {
                StatType = pickedEntry.StatType,
                Value = pickedEntry.BaseValue
            });
        }
    }

    private static void ApplySubStats(EquipmentData data, EquipmentInstance instance)
    {
        // (기존 InventoryManager에 작성하신 로직과 동일하게 유지)
        StatPoolData subPool = Managers.Data.GetData<int, StatPoolData>(data.SubStatPoolID);
        if (subPool == null || subPool.Entries.Count == 0) return;

        // 아이템 등급별 서브 스탯 개수 설정 (로스트아크나 원신 스타일)
        int subStatCount = Managers.Data.GetData<ItemGrade, GradeConfig>(data.Grade).InitialSubStatCount;

        //switch (data.Grade)
        //{
        //    case ItemGrade.Common: subStatCount = 0; break;
        //    case ItemGrade.Uncommon: subStatCount = 1; break;
        //    case ItemGrade.Rare: subStatCount = 2; break;
        //    case ItemGrade.Epic: subStatCount = 3; break;
        //    case ItemGrade.Legendary: subStatCount = 4; break;
        //    case ItemGrade.Mythic: subStatCount = 4; break; // Mythic은 수치가 더 높거나 고정옵일 수 있음
        //}

        // 중복 스탯 방지를 위한 리스트 복사
        List<StatPoolEntry> availableEntries = new List<StatPoolEntry>(subPool.Entries);

        for (int i = 0; i < subStatCount; i++)
        {
            if (availableEntries.Count == 0) break;

            StatPoolEntry pickedEntry = PickRandomEntryByWeight(availableEntries);
            if (pickedEntry != null)
            {
                instance.SubStats.Add(new StatOption()
                {
                    StatType = pickedEntry.StatType,
                    Value = pickedEntry.BaseValue
                });

                // 동일한 스탯이 중복으로 뜨는 것을 막으려면 리스트에서 제거
                availableEntries.Remove(pickedEntry);
            }
        }
    }

    private static StatPoolEntry PickRandomEntryByWeight(List<StatPoolEntry> entries)
    {
        // (기존 InventoryManager에 작성하신 로직과 동일하게 유지)
        int totalWeight = 0;
        foreach (var entry in entries)
        {
            totalWeight += entry.Weight;
        }

        // 1부터 totalWeight 사이의 난수 발생
        int randomValue = UnityEngine.Random.Range(1, totalWeight + 1);
        int currentWeight = 0;

        foreach (var entry in entries)
        {
            currentWeight += entry.Weight;
            if (randomValue <= currentWeight)
            {
                return entry;
            }
        }

        return null; // 논리상 여기까지 오지 않음
    }
}
