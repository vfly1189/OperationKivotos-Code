using Org.BouncyCastle.Asn1.X509.Qualified;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemGrade
{
    Common,     // 회색
    Uncommon,   // 녹색
    Rare,       // 파란색
    Epic,       // 보라색
    Legendary,  // 주황색
    Mythic      // 빨간색
}

public enum ItemCategory
{
    Equipment,
    Consumable,
    Material
}

[Serializable]
public class InventorySlot
{
    public int itemID;
    public int Amount;
}

public class InventoryManager
{
    public Dictionary<ItemCategory, List<InventorySlot>> Inventory { get; private set; }


    // UI 갱신용 이벤트 (어떤 탭이 업데이트 되었는지 매개변수로 전달)
    public event Action<ItemCategory> OnInventoryUpdated;

    public void Init()
    {
        Inventory = new Dictionary<ItemCategory, List<InventorySlot>>()
        {
            { ItemCategory.Equipment, new List<InventorySlot>()},
            { ItemCategory.Consumable, new List<InventorySlot>()},
            { ItemCategory.Material, new List<InventorySlot>()}
        };

        AddItem(10002, ItemCategory.Equipment, 1);
    }

    // 1. DataManager에서 카테고리에 맞는 MaxStack을 안전하게 가져오는 헬퍼 함수
    private int GetMaxStack(int itemID, ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Equipment:
                return 1; // 장비는 무조건 1개만 (안 겹침)

            case ItemCategory.Consumable:
                var consumeData = Managers.Data.GetData<int, ConsumableData>(itemID);
                return consumeData != null ? consumeData.MaxStack : 1;

            case ItemCategory.Material:
                var materialData = Managers.Data.GetData<int, MaterialData>(itemID);
                return materialData != null ? materialData.MaxStack : 1;

            default:
                return 1;
        }
    }


    // 2. 완벽한 Stack 분할 로직이 적용된 AddItem
    public void AddItem(int itemID, ItemCategory category, int amount = 1)
    {
        if (amount <= 0) return;

        List<InventorySlot> targetList = Inventory[category];
        int maxStack = GetMaxStack(itemID, category);

        // 추가해야 할 남은 수량
        int remainingAmount = amount;

        // 소모품이나 재료라면, 기존에 덜 채워진(MaxStack 미만) 슬롯들을 찾아 채워넣습니다.
        if (category != ItemCategory.Equipment)
        {
            foreach (var slot in targetList)
            {
                if (slot.itemID == itemID && slot.Amount < maxStack)
                {
                    // 현재 슬롯에 추가할 수 있는 여유 공간
                    int spaceLeft = maxStack - slot.Amount;

                    if (remainingAmount <= spaceLeft)
                    {
                        // 남은 수량이 여유 공간보다 작거나 같으면 전부 넣고 끝!
                        slot.Amount += remainingAmount;
                        remainingAmount = 0;
                        break; // 루프 탈출
                    }
                    else
                    {
                        // 남은 수량이 더 많으면, 일단 이 슬롯을 가득(Max) 채우고 남은 건 다음 슬롯으로 넘김
                        slot.Amount = maxStack;
                        remainingAmount -= spaceLeft;
                    }
                }
            }
        }

        // 기존 슬롯들을 다 채우고도(혹은 장비라서) 남은 아이템이 있다면, 새로운 슬롯을 생성해야 함
        while (remainingAmount > 0)
        {
            // 한 번에 만들 새 슬롯에 들어갈 개수 (최대 MaxStack만큼)
            int addAmount = Mathf.Min(remainingAmount, maxStack);

            // TODO: 여기서 인벤토리 최대 칸수(Max Slots) 제한 체크를 할 수도 있습니다.

            targetList.Add(new InventorySlot { itemID = itemID, Amount = addAmount });
            remainingAmount -= addAmount;
        }

        // 처리가 모두 끝나면 UI 갱신 이벤트 호출
        OnInventoryUpdated?.Invoke(category);
    }
}
