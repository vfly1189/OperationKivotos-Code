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

    [Header("Prefabs")]
    public GameObject originalPrefab;
    public GameObject selectPrefab;
    public GameObject inGamePrefab;

    [Header("Voices")]
    public AudioClip[] formationInVoices; // "Formation_In_1", "Formation_In_2" 등

    [Header("SkillIcon")]
    public Sprite qSkillIcon;
    public Sprite eSkillIcon;

    [Header("UI Colors")]
    public Color energyFillColor;   // 게이지 차오르는 색 (밝음)
    public Color ultimateGlowColor; // 뒤에서 일렁이는 이펙트 색 (진함)

}