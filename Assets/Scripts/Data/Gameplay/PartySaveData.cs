using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PartySaveData
{
    public string partyId;          // "Abydos", "Gehenna" 같은 키

    public PartyRuntimeData party;  // 레벨/exp, 클리어 정보 등

    public List<CharacterSaveData> characters;

    public InventorySaveData inventory;
    public EquipmentSaveData equipment;

    public WalletSaveData wallet;
}

[Serializable]
public class PartyRuntimeData
{
    public int partyLevel;
    public float partyCurrentExp;
}

[Serializable]
public class CharacterSaveData
{
    public int characterId;
    public int weaponLevel;
    public float currentHp;
}

[Serializable]
public class InventorySaveData
{
    public List<InventorySlotEntry> equipments = new List<InventorySlotEntry>();
    public List<InventorySlotEntry> consumables = new List<InventorySlotEntry>();
    public List<InventorySlotEntry> materials = new List<InventorySlotEntry>();
}

[Serializable]
public class InventorySlotEntry
{
    public int slotIndex;       // 배열에서 몇 번 슬롯인지
    public InventorySlot slot;  // 실제 아이템 데이터
}


[Serializable]
public class EquipmentSaveData
{
    public List<EquippedSlotEntry> equippedSlots = new List<EquippedSlotEntry>();
}

[Serializable]
public class EquippedSlotEntry
{
    public EquipType slotType;   // 어떤 부위인지
    public InventorySlot slot;   // 장착된 장비 데이터
}


[Serializable]
public class WalletSaveData
{
    //public int credit;
    public List<WalletDataEntry> currencyData = new List<WalletDataEntry>();
}

[Serializable]
public class WalletDataEntry
{
    public CurrencyType currencyType;   // 어떤 부위인지
    public int amount;   // 장착된 장비 데이터
}

