using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/ItemData/Weapon")]
public class WeaponDataSO : ItemDataSO
{

    // [추가] 특정 레벨의 데이터를 가져오는 함수
    public WeaponData GetLevelData(int level)
    {
        // DataManager에 "WeaponID_Level" 키로 요청
        // 예: "Weapon_Hoshino_1"
        string key = $"{itemID}_{level}";

        WeaponData data = Managers.Data.GetData<string, WeaponData>(key);

        if (data != null)
        {
            return data;
        }

        Debug.LogError($"Weapon Data Not Found! Key: {key}");
        return null;
    }

    // 현재 레벨의 공격력 보너스만 바로 가져오기
    public float GetAttackBonus(int level)
    {
        var data = GetLevelData(level);
        return data != null ? data.AttackBonus : 0f;
    }
}