using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Data/CharacterData")]
public class CharacterDataSO : ScriptableObject
{
    [Header("Info")]
    public string id;           // "char_hoshino"
    public string nameEN;
    public string nameKR;
    public Sprite Portrait;

    [Header("Base Stats")]
    public float MaxHp;
    public float Attack;
    public float Defense;
    public float MoveSpeed;
    public float AttackSpeed;
    public float MaxEnergy;
    public float MaxExp;
    public float QSkillCoolTime;
    public float ESkillCoolTime;

    [Header("Growth Stats (Per Level)")]
    public float MaxHpGrowth;   // 레벨당 체력 증가량
    public float AttackGrowth;  // 레벨당 공격력 증가량
    public float DefenseGrowth; // 레벨당 방어력 증가량
    public float ExpGrowth;     // 레벨당 경험치최대치 증가량

    [Header("Prefabs")]
    public GameObject originalPrefab;
    public GameObject selectPrefab;
    public GameObject inGamePrefab;
    public GameObject bulletPrefab;

    [Header("Voices")]
    public AudioClip[] formationInVoices; // "Formation_In_1", "Formation_In_2" 등
    public AudioClip[] battleInVoices;
   

    [Header("SkillIcon")]
    public Sprite qSkillIcon;
    public Sprite eSkillIcon;

    [Header("UI Colors")]
    public Color energyFillColor;   // 게이지 차오르는 색 (밝음)
    public Color ultimateGlowColor; // 뒤에서 일렁이는 이펙트 색 (진함)

}