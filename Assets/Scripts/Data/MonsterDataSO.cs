using UnityEngine;

[CreateAssetMenu(fileName = "NewMonsterData", menuName = "Data/MonsterData")]
public class MonsterDataSO : MonoBehaviour
{
    [Header("Info")]
    public string id;           //
    public string nameEN;
    public string nameKR;

    [Header("Base Stats")]
    public float MaxHp;
    public float Attack;
    public float Defense;

    [Header("Prefabs")]
    public GameObject originalPrefab;
}
