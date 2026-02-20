//using JetBrains.Annotations;
using UnityEngine;

// 아이템 타입 정의
public enum ItemType { Weapon, Artifact }
public enum ArtifactSlot { None, Slot1_Swap, Slot2_ESkill, Slot3_QSkill }


[CreateAssetMenu(fileName = "NewItemData", menuName = "Data/ItemData/Base")]
public class ItemDataSO : ScriptableObject
{
    public string itemID;
    public string itemName;
    public Sprite icon;
    //[TextArea] public string description;
    public ItemType itemType;
    public int characterId; // 특정 캐릭터 전용 (0이면 공용, 혹은 특정 ID)
}



//[CreateAssetMenu(fileName = "NewArtifactData", menuName = "Data/ItemData/Artifact")]
//public class ArtifactDataSO : ItemDataSO
//{
//    public ArtifactSlot slotType;

//    [Header("Tier Data (Index 0 = Tier1, 3 = Tier4)")]
//    public ArtifactTierData[] tierData;
//}

//[System.Serializable]
//public class ArtifactTierData
//{
//    // 티어별 효과 수치 (예: 교체시 공증 10% -> 15% -> 20%)
//    public float effectValue;
//    public float duration;
//    public int requiredGold;
//    public int requiredStones;
//}

//// 고유 아이템 (해금 시스템)
//[CreateAssetMenu(fileName = "New UniqueItem", menuName = "Items/UniqueItem")]
//public class UniqueItemData : ItemBase
//{
//    public int tier; // 1~3
//    public int requiredFragments; //요구 재료
//}