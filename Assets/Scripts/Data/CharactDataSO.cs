using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Data/CharacterData")]
public class CharacterDataSO : ScriptableObject
{
    [Header("Info")]
    public string CharacterName;
    public Sprite Portrait;

    [Header("Base Stats")]
    public float MaxHp;
    public float Attack;
    public float Defense;
    public float MoveSpeed;
    public float AttackSpeed;
    public float MaxEnergy;
}