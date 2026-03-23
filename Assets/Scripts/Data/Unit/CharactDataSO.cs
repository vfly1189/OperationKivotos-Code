using UnityEngine;
using UnityEngine.AddressableAssets; // 필수

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Data/CharacterData")]
public class CharacterDataSO : ScriptableObject
{
    // ==========================================
    // [1] 엑셀에서 자동 주입될 데이터 (수동 수정 X)
    // 엑셀의 컬럼명과 1:1 매칭되는 변수들입니다.
    // ==========================================
    [Header("Excel Data - Info")]
    public int id;              // int_ID
    public string key;           // 예: char_Hoshino -> string_ID
    public string nameKR;       // 예: 호시노
    // (nameEN은 엑셀에 없으므로 일단 제외하거나 나중에 엑셀에 추가하시면 됩니다)

    [Header("Excel Data - Base Stats (Lv.1)")]
    public float baseHp;              // Base_HP
    public float baseAttack;          // Base_ATK
    public float baseDefense;         // Base_DEF
    public float baseCritRate;        // Base_CritRate (예: 0.05)
    public float baseCritDamage;      // Base_CritDMG (예: 1.5)
    public float baseEnergyRecharge;  // Base_EnergyRecharge (예: 1)
    public float baseMoveSpeed;       // Base_MoveSpeed (예: 4.5)

    [Header("Excel Data - Growth Stats (Per Level)")]
    public float hpGrowth;            // Growth_HP
    public float attackGrowth;        // Growth_ATK
    public float defenseGrowth;       // Growth_DEF

    [Header("Excel Data - Fixed Stats")]
    public float maxEnergy;           // Max_Energy


    // ==========================================
    // [2] 유니티 에디터에서 개발자가 직접 연결할 에셋들
    // ==========================================
    [Header("Assets - UI")]
    public AssetReferenceSprite  Emblem;
    public AssetReferenceSprite  Portrait;
    public AssetReferenceSprite  qSkillIcon;
    public AssetReferenceSprite  eSkillIcon;
    public Color energyFillColor;
    public Color ultimateGlowColor;

    [Header("Assets - Prefabs")]
    public GameObject originalPrefab;
    public AssetReferenceGameObject selectPrefab;
    public AssetReferenceGameObject inGamePrefab;
    public GameObject bulletPrefab;

    [Header("Assets - Voices")]
    public AssetReferenceT<AudioClip>[] formationInVoices;
    public AssetReferenceT<AudioClip>[] battleInVoices;
    public AssetReferenceT<AudioClip>[] battleVictoryVocies;

    // (쿨타임이나 공격속도는 무기나 스킬 쪽에 종속시킬지, 캐릭터 SO에 남길지 기획에 따라 추가하시면 됩니다)
    [Header("Assets - Combat Settings")]
    public float AttackSpeed = 1.0f;
    public float QSkillCoolTime = 15f;
    public float ESkillCoolTime = 8f;


    // ==========================================
    // 편의성 함수 (해당 캐릭터의 특정 레벨 순수 '기본 스탯' 구하기)
    // ==========================================
    public float GetLevelHp(int level) => baseHp + (hpGrowth * (level - 1));
    public float GetLevelAttack(int level) => baseAttack + (attackGrowth * (level - 1));
    public float GetLevelDefense(int level) => baseDefense + (defenseGrowth * (level - 1));
}