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

        //// 해당 부위에 이미 장착된 장비가 있다면 벗어서 인벤토리에 넣기
        //// 서로 교체하는 방식이 낫지 않나?
        //if (_equippedItem.ContainsKey(type) && _equippedItem[type] != null && !_equippedItem[type].IsEmpty)
        //{
        //    UnEquip(type);
        //}

        //// 새 장비를 _equippedItem 딕셔너리에 등록 (값 복사)
        //InventorySlot newEquip = new InventorySlot { itemID = invenSlot.itemID, Amount = 1 };
        //_equippedItem[type] = newEquip;

        //// 3. 인벤토리에서 해당 슬롯 비우기
        //invenSlot.Clear();

        ////인벤에서 UI 갱신해야됨
        ////전체를 갱신할 필요가있나? -> 해당 슬롯들만 갱신하면되는거아닌가?
        //OnInventoryChanged?.Invoke(ItemCategory.Equipment); // 인벤토리 탭 갱신

        ////UI 갱신 및 스탯 재계산 이벤트 호출
        //OnEquipmentChanged?.Invoke(type, newEquip, null);

        // 새 장비의 데이터를 미리 복사해두고 인벤토리 슬롯을 먼저 비웁니다. (공간 확보)
        InventorySlot newEquip = new InventorySlot { itemID = invenSlot.itemID, Amount = 1 };
        invenSlot.Clear();

        // 그 다음 기존 장비를 벗깁니다. (확보된 빈 공간으로 안전하게 들어감)
        if (_equippedItem.ContainsKey(type) && _equippedItem[type] != null && !_equippedItem[type].IsEmpty)
        {
            UnEquip(type);
        }

        // 3. 새 장비 장착 적용
        _equippedItem[type] = newEquip;

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

        Debug.Log("너리너ㅏㅣ런이렁");

        InventorySlot unequippedItem = _equippedItem[type];

        // 1. 벗은 장비를 인벤토리에 다시 추가
        Managers.Inventory.AddItem(unequippedItem.itemID, ItemCategory.Equipment, 1);

        // 2. 장착 슬롯 비우기
        _equippedItem[type] = null;



        //UI 갱신 및 스탯 재계산 이벤트 호출
        OnEquipmentChanged?.Invoke(type, null, unequippedItem);
    }

}
