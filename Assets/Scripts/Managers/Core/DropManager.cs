using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class DropManager
{
    // 드랍 테이블 ID를 받아 실제로 아이템을 뽑고 인벤토리에 넣은 뒤, 획득한 목록을 반환
    public void RollAndGiveDropItems(int dropTableId)
    {
        DropTable table = Managers.Data.GetData<int, DropTable>(dropTableId);
        if (table == null || table.Entries.Count == 0) return;

        // 테이블에 명시된 Rolls(굴림 횟수)만큼 가챠를 돌림
        for (int r = 0; r < table.Rolls; r++)
        {
            DropTableEntry pickedEntry = PickRandomEntry(table);

            if (pickedEntry != null)
            {
                GiveReward(pickedEntry);
            }
        }
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
    private async void GiveReward(DropTableEntry entry)
    {
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

                BaseItemData baseData = Managers.Data.GetItemData(finalItemId, ItemCategory.Equipment);
                EquipmentData data = baseData as EquipmentData;

                Sprite icon = await Managers.Resource.LoadAsync<Sprite>(data.IconKey);

                UnityEngine.Color backgroundColor = ColorDict.GetGradeColor(data.Grade);

                // UI 알림 띄우기
                Managers.UI.ShowLootToast(data.Name, amount, icon, backgroundColor);
            }
        }
        else if (entry.RewardType == DropTableDefine.RewardType.Material)
        {
            // 재료는 ItemID가 명확하게 들어있음
            Managers.Inventory.AddItem(entry.ItemID, ItemCategory.Material, amount);

            BaseItemData baseData = Managers.Data.GetItemData(entry.ItemID, ItemCategory.Material);
            MaterialData data = baseData as MaterialData;

            Sprite icon = await Managers.Resource.LoadAsync<Sprite>(data.IconKey);
            UnityEngine.Color backgroundColor = ColorDict.GetGradeColor(data.Grade);
            // UI 알림 띄우기
            Managers.UI.ShowLootToast(data.Name, amount, icon, backgroundColor);
        }
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

        Debug.LogWarning($"[DropManager] Tier {tier}에 해당하는 장비가 없습니다.");
        return -1;
    }
}
