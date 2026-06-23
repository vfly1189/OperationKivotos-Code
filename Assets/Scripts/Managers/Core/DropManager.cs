using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;


public class DropManager
{
    // 반환형을 List<RewardInfo>로 변경
    public List<InventorySlot> RollAndGiveDropItems(int dropTableId, bool showToast = true)
    {
        List<InventorySlot> results = new List<InventorySlot>();

        DropTable table = Managers.Data.GetData<int, DropTable>(dropTableId);
        if (table == null || table.Entries.Count == 0) return results;

        for (int r = 0; r < table.Rolls; r++)
        {
            DropTableEntry pickedEntry = PickRandomEntry(table);
            if (pickedEntry != null)
            {
                // 실제 인벤토리에 넣고 결과를 받아옴
                InventorySlot info = GiveReward(pickedEntry, showToast);
                if (info.itemID > 0)
                {
                    results.Add(info);
                }
            }
        }
        return results;
    }

    // 가중치(Weight) 기반으로 아이템 하나를 뽑는 핵심 함수
    private DropTableEntry PickRandomEntry(DropTable table)
    {
        // TotalWeight는 앞서 DataManager(또는 SO)에서 미리 계산해둔 값을 사용
        int totalWeight = table.TotalWeight;
        if (totalWeight <= 0) return null;

        int randomValue = Random.Range(0, totalWeight); // 0 ~ (totalWeight - 1)
        int currentWeight = 0;

        foreach (var entry in table.Entries)
        {
            currentWeight += entry.Weight;
            if (randomValue < currentWeight)
            {
                return entry; // 당첨!
            }
        }

        return null;
    }

    // 뽑힌 엔트리를 분석해서 실제 인벤토리에 넣어주고 UI 띄우기
    private InventorySlot GiveReward(DropTableEntry entry, bool showToast)
    {
        InventorySlot result = new InventorySlot();

        // 1. 개수 결정 (Min ~ Max)
        int amount = Random.Range(entry.MinCount, entry.MaxCount + 1);

        // 2. 타입에 따라 처리
        if (entry.RewardType == DropTableDefine.RewardType.Equipment)
        {
            // 장비의 경우 테이블에 ItemID가 -1이고 Tier만 있다면,
            // 해당 Tier에 맞는 장비 목록 중에서 랜덤으로 하나를 다시 골라야 함
            int finalItemId = DetermineEquipmentIdByTier(entry.Tier);
            if (finalItemId > 0)
            {
                // 인벤토리에 추가
                Managers.Inventory.AddEquipmentSlot(EquipmentFactory.CreateEquipment(finalItemId));

                result.itemID = finalItemId;
                result.Amount = amount;
                result.itemCategory = ItemCategory.Equipment;

                if(showToast)
                {
                    BaseItemData baseData = Managers.Data.GetItemData(finalItemId, ItemCategory.Equipment);
                    EquipmentData data = baseData as EquipmentData;
                    UnityEngine.Color backgroundColor = ColorDict.GetGradeColor(data.Grade);

                    // UI 알림 띄우기
                    //Managers.UI.ShowLootToast(data.Name, amount, icon, backgroundColor);
                    UI_LootNotification.ShowToast(ItemCategory.Equipment, data.Name, amount, data.IconKey, backgroundColor).Forget();
                }
            }
        }
        else if (entry.RewardType == DropTableDefine.RewardType.Material)
        {
            // 재료는 ItemID가 명확하게 들어있음
            Managers.Inventory.AddItem(entry.ItemID, ItemCategory.Material, amount);

            result.itemID = entry.ItemID;
            result.Amount = amount;
            result.itemCategory = ItemCategory.Material;

            if( showToast)
            {
                BaseItemData baseData = Managers.Data.GetItemData(entry.ItemID, ItemCategory.Material);
                MaterialData data = baseData as MaterialData;
                UnityEngine.Color backgroundColor = ColorDict.GetGradeColor(data.Grade);

                // UI 알림 띄우기
                //Managers.UI.ShowLootToast(data.Name, amount, icon, backgroundColor);
                UI_LootNotification.ShowToast(ItemCategory.Material, data.Name, amount, data.IconKey, backgroundColor).Forget();
            }
        }

        return result;
    }

    // 티어 기반으로 무작위 장비를 하나 뽑아오는 함수
    private int DetermineEquipmentIdByTier(int tier)
    {
        // 1. DataManager에서 미리 만들어둔 '티어별 장비 인덱스'를 가져옴
        //    (타입 식별자로 아까 만든 마커 클래스 사용)
        var tierIndexDict = Managers.Data.GetDict<int, List<int>, EquipmentTierIndex>();


        // 2. O(1) 검색으로 해당 티어의 장비 ID 리스트를 바로 획득
        if (tierIndexDict != null && tierIndexDict.TryGetValue(tier, out List<int> candidateIds))
        {
            if (candidateIds.Count > 0)
            {
                // 3. 리스트 안에서 랜덤으로 하나 뽑기 (역시 O(1))
                return candidateIds[Random.Range(0, candidateIds.Count)];
            }
        }

        GameLog.LogWarning($"[DropManager] Tier {tier}에 해당하는 장비가 없습니다.");
        return -1;
    }
}
