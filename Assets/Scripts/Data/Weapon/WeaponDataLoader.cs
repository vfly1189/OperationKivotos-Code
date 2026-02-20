using System.Collections.Generic;
using UnityEngine;

[System.Serializable] // <<<<< 이거 필수!
public class WeaponData
{
    // CSV 순서: ID, Level, AttackBonus, HpBonus, CostGold, CostStone
    public string ID;
    public int Level;
    public float AttackBonus;
    public float HpBonus;
    public int CostGold;
    public int CostStones;
}

[System.Serializable] // <<<<< 여기도 확인!
// 무기 데이터 로더 (Loader)
// JSON을 읽어서 Dictionary<string, WeaponData>로 변환
public class WeaponDataLoader : ILoader<string, WeaponData>
{
    public List<WeaponData> stats = new List<WeaponData>();

    public Dictionary<string, WeaponData> MakeDict()
    {
        Dictionary<string, WeaponData> dict = new Dictionary<string, WeaponData>();
        foreach (WeaponData stat in stats)
        {
            // Key 생성:  (예: Weapon_Hoshino_2)
            string key = $"{stat.ID}_{stat.Level}";
            dict.Add(key, stat);
        }
        return dict;
    }
}