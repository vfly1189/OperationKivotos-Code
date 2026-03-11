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

    // 장비일 경우에만 할당되는 고유 데이터
    public EquipmentInstance EquipInstance;

    public bool IsEmpty => itemID == 0 || Amount <= 0;

    public bool IsEquipment => itemID >= 10000 && itemID <= 19999;

    public void Clear()
    {
        itemID = 0;
        Amount = 0;
    }
}

// 1. 장비가 실제로 생성될 때 가지는 '개별 스탯' 데이터
[Serializable]
public class EquipmentInstance
{
    public List<StatOption> MainStats = new List<StatOption>(); // 메인 옵션 (보통 1개)
    public List<StatOption> SubStats = new List<StatOption>();  // 서브 옵션 (랜덤)
    public int UpgradeLevel = 0;
}

[Serializable]
public class StatOption
{
    public EStatType StatType;
    public float Value;
    public int UpgradeCount = 0;

    // UI에 보여줄 때 편하도록 프로퍼티 추가
    public string GetStatString()
    {
        // 예: MaxHP_Percent 이면 "최대 체력 +5%" 형태로 반환
        bool isPercent = StatType.ToString().Contains("Percent") ||
                         StatType == EStatType.CritRate ||
                         StatType == EStatType.CritDamage ||
                         StatType == EStatType.EnergyRegen;

        if (isPercent) return $"{(Value * 100):0.##}%";
        return $"{Value}";
    }
}

public class InventoryManager
{
    public int _maxSlotCount = 100;

    public Dictionary<ItemCategory, InventorySlot[]> Inventory { get; private set; }

    // UI 갱신용 이벤트 (어떤 탭이 업데이트 되었는지 매개변수로 전달)
    public event Action<ItemCategory> OnInventoryUpdated;

    public void Init()
    {
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

        Managers.Equipment.OnInventoryChanged -= RefreshUI;
        Managers.Equipment.OnInventoryChanged += RefreshUI;
    }

    void RefreshUI(ItemCategory category)
    {
        OnInventoryUpdated?.Invoke(category);
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
        EquipmentInstance tempEquip = slotA.EquipInstance;

        slotA.itemID = slotB.itemID;
        slotA.Amount = slotB.Amount;
        slotA.EquipInstance = slotB.EquipInstance;

        slotB.itemID = tempID;
        slotB.Amount = tempAmount;
        slotB.EquipInstance = tempEquip;

        OnInventoryUpdated?.Invoke(category);
    }

    // 장비 전용 Add 함수 (미리 만들어진 슬롯을 넘겨받음)
    public void AddEquipmentSlot(InventorySlot newEquipSlot)
    {
        InventorySlot[] targetArray = Inventory[ItemCategory.Equipment];

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

        if (emptyIndex == -1)
        {
            Debug.LogWarning("장비 인벤토리가 가득 찼습니다! (우편함 발송 등 처리 필요)");
            return;
        }

        // 인벤토리에 참조 복사 (구조체면 값 복사, 클래스면 참조)
        targetArray[emptyIndex] = newEquipSlot;

        OnInventoryUpdated?.Invoke(ItemCategory.Equipment);
    }

    // 몬스터 드롭 시 호출되는 함수!
    // 랜덤 스탯을 굴려서 InventorySlot을 완성한 뒤 인벤토리에 넣습니다.
    public void DropEquipment(int itemID)
    {
        EquipmentData data = Managers.Data.GetData<int, EquipmentData>(itemID);
        if (data == null) return;

        InventorySlot newSlot = new InventorySlot();
        newSlot.itemID = itemID;
        newSlot.Amount = 1;
        newSlot.EquipInstance = new EquipmentInstance();

        // 1. 메인 스탯 부여
        ApplyMainStat(data, newSlot.EquipInstance);

        // 2. 서브 스탯 랜덤 부여 (등급별로 개수 다름)
        ApplySubStats(data, newSlot.EquipInstance);

        // 3. 인벤토리에 넣기
        AddEquipmentSlot(newSlot);
    }

    private void ApplyMainStat(EquipmentData data, EquipmentInstance instance)
    {
        StatPoolData mainPool = Managers.Data.GetData<int, StatPoolData>(data.MainStatPoolID);
        if (mainPool == null || mainPool.Entries.Count == 0) return;

        // 메인 스탯 풀도 여러 개일 수 있습니다. (예: Tier1 General Random은 여러 개 중 1개 픽)
        StatPoolEntry pickedEntry = PickRandomEntryByWeight(mainPool.Entries);
        if (pickedEntry != null)
        {
            instance.MainStats.Add(new StatOption()
            {
                StatType = pickedEntry.StatType,
                Value = pickedEntry.BaseValue // 강화 안 된 기본 수치
            });
        }
    }

    private void ApplySubStats(EquipmentData data, EquipmentInstance instance)
    {
        StatPoolData subPool = Managers.Data.GetData<int, StatPoolData>(data.SubStatPoolID);
        if (subPool == null || subPool.Entries.Count == 0) return;

        // 아이템 등급별 서브 스탯 개수 설정 (로스트아크나 원신 스타일)
        int subStatCount = 0;
        switch (data.Grade)
        {
            case ItemGrade.Common: subStatCount = 0; break;
            case ItemGrade.Uncommon: subStatCount = 1; break;
            case ItemGrade.Rare: subStatCount = 2; break;
            case ItemGrade.Epic: subStatCount = 3; break;
            case ItemGrade.Legendary: subStatCount = 4; break;
            case ItemGrade.Mythic: subStatCount = 4; break; // Mythic은 수치가 더 높거나 고정옵일 수 있음
        }

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

    // 핵심: 가중치(Weight) 기반 랜덤 뽑기 알고리즘
    private StatPoolEntry PickRandomEntryByWeight(List<StatPoolEntry> entries)
    {
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
