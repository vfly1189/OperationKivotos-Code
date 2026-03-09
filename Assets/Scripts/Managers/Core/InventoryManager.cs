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

    public bool IsEmpty => itemID == 0 || Amount <= 0;
    public void Clear()
    {
        itemID = 0;
        Amount = 0;
    }
}

public class InventoryManager
{
    public int _maxSlotCount = 30;


    //public Dictionary<ItemCategory, List<InventorySlot>> Inventory { get; private set; }
    public Dictionary<ItemCategory, InventorySlot[]> Inventory { get; private set; }

    // UI 갱신용 이벤트 (어떤 탭이 업데이트 되었는지 매개변수로 전달)
    public event Action<ItemCategory> OnInventoryUpdated;

    public void Init()
    {
        //Inventory = new Dictionary<ItemCategory, List<InventorySlot>>()
        //{
        //    { ItemCategory.Equipment, new List<InventorySlot>()},
        //    { ItemCategory.Consumable, new List<InventorySlot>()},
        //    { ItemCategory.Material, new List<InventorySlot>()}
        //};

        Inventory = new Dictionary<ItemCategory, InventorySlot[]>();

        foreach (ItemCategory category in Enum.GetValues(typeof(ItemCategory)))
        {
            InventorySlot[] slots = new InventorySlot[_maxSlotCount];
            for(int i=0; i < slots.Length; i++)
            {
                slots[i] = new InventorySlot();
            }
            Inventory[category] = slots;
        }

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

        InventorySlot[] targetArray = Inventory[category];
        int maxStack = GetMaxStack(itemID, category);

        // 추가해야 할 남은 수량
        int remainingAmount = amount;

        // 1. 소모품이나 재료라면, 기존에 덜 채워진(MaxStack 미만) 슬롯들을 찾아 채워넣습니다.
        if (category != ItemCategory.Equipment)
        {
            foreach (var slot in targetArray)
            {
                if (slot.itemID == itemID && slot.Amount < maxStack)
                {
                    int spaceLeft = maxStack - slot.Amount;

                    if (remainingAmount <= spaceLeft)
                    {
                        slot.Amount += remainingAmount;
                        remainingAmount = 0;
                        break;
                    }
                    else
                    {
                        slot.Amount = maxStack;
                        remainingAmount -= spaceLeft;
                    }
                }
            }
        }

        // 2. 남은 아이템이 있다면, 앞에서부터 빈 슬롯(IsEmpty)을 찾아 채워넣습니다.
        while (remainingAmount > 0)
        {
            int addAmount = Mathf.Min(remainingAmount, maxStack);

            // 빈 슬롯 찾기
            int emptyIndex = -1;
            for (int i = 0; i < targetArray.Length; i++)
            {
                if (targetArray[i].IsEmpty)
                {
                    emptyIndex = i;
                    break;
                }
            }

            // 빈 슬롯이 없는 경우 (인벤토리가 가득 참)
            if (emptyIndex == -1)
            {
                Debug.LogWarning($"[{category}] 인벤토리가 가득 차서 더 이상 획득할 수 없습니다. (남은 수량: {remainingAmount})");
                // TODO: 남은 수량만큼 바닥에 드랍하거나 우편함으로 보내는 로직 추가 필요
                break;
            }

            // 찾은 빈 슬롯에 아이템 할당
            targetArray[emptyIndex].itemID = itemID;
            targetArray[emptyIndex].Amount = addAmount;

            remainingAmount -= addAmount;
        }

        // 처리가 모두 끝나면 UI 갱신 이벤트 호출
        OnInventoryUpdated?.Invoke(category);
    }
    public void SwapItems(ItemCategory category, int indexA, int indexB)
    {
        Debug.Log($"Index : {indexA} , {indexB}");

        if (indexA == indexB) return;

        InventorySlot slotA = Inventory[category][indexA];
        InventorySlot slotB = Inventory[category][indexB];

        // 같은 아이템이라면 병합 로직 (선택 사항)
        if (!slotA.IsEmpty && !slotB.IsEmpty && slotA.itemID == slotB.itemID && category != ItemCategory.Equipment)
        {
            int maxStack = GetMaxStack(slotA.itemID, category);
            int spaceLeft = maxStack - slotB.Amount;

            if (spaceLeft > 0)
            {
                int moveAmount = Mathf.Min(slotA.Amount, spaceLeft);
                slotB.Amount += moveAmount;
                slotA.Amount -= moveAmount;

                if (slotA.Amount <= 0) slotA.Clear();

                OnInventoryUpdated?.Invoke(category);
                return;
            }
        }

        // 단순 스왑 (Swap)
        int tempID = slotA.itemID;
        int tempAmount = slotA.Amount;

        slotA.itemID = slotB.itemID;
        slotA.Amount = slotB.Amount;

        slotB.itemID = tempID;
        slotB.Amount = tempAmount;

        OnInventoryUpdated?.Invoke(category);
    }



    //외부에서 호출할 편의 함수들

    //아이템 갯수를 리턴해주는 함수
    // 아이템 갯수를 리턴해주는 함수 (가독성 정리)
    public int GetItemCount(ItemCategory category, int itemID)
    {
        int count = 0;
        foreach (var slot in Inventory[category])
        {
            if (!slot.IsEmpty && slot.itemID == itemID)
            {
                count += slot.Amount;
            }
        }
        return count;
    }

    // 아이템 소모 함수 (초과 소모 버그 수정)
    public bool ConsumeMaterial(int itemID, int amount)
    {
        // 애초에 총량이 부족하면 false
        if (GetItemCount(ItemCategory.Material, itemID) < amount)
            return false;

        int remainAmount = amount;

        foreach (var slot in Inventory[ItemCategory.Material])
        {
            if (!slot.IsEmpty && slot.itemID == itemID)
            {
                if (slot.Amount <= remainAmount)
                {
                    remainAmount -= slot.Amount;
                    slot.Clear();
                }
                else
                {
                    slot.Amount -= remainAmount;
                    remainAmount = 0; // [수정됨] 깎고 나서 남은 요구량을 0으로 처리
                }
            }

            // 다 깎았다면 더 이상 반복문 돌 필요 없이 즉시 탈출
            if (remainAmount <= 0) break;
        }

        OnInventoryUpdated?.Invoke(ItemCategory.Material);
        return true;
    }
}
