using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[System.Serializable] // <<<<< 이거 필수!
public class WeaponLevelStat
{
    public int Level;
    public float Attack;
    public float HP;
    public float CritRate;
    public float CritDmg;
}


[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/WeaponData")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Excel Data - Info")]
    public int id;              // 2000
    public string key;          // wpn_Hoshino
    public string weaponName;   // 호루스의 눈
    public int ownerCharID;     // 1000

    [Header("Assets - Visuals & Prefabs")]
    public AssetReferenceSprite weaponIcon;

    [Header("Excel Data - Stats Array (Lv.1 ~ Max)")]
    // 엑셀 파싱 시점에 Base 스탯 + Growth 스탯을 미리 계산해서 25개 칸에 꽉 채워넣습니다.
    // 인덱스 0 = 1레벨, 인덱스 1 = 2레벨 ...
    public WeaponLevelStat[] levelStats;

    // ==========================================
    // 런타임에서 특정 레벨 스탯 빼오기용 함수
    // ==========================================
    public WeaponLevelStat GetStatByLevel(int level)
    {
        // 배열 인덱스는 0부터 시작하므로 (level - 1)
        int index = level - 1;

        if (levelStats != null && index >= 0 && index < levelStats.Length)
        {
            return levelStats[index];
        }

        Debug.LogError($"[{weaponName}] {level} 레벨의 스탯 데이터가 없습니다.");
        return default;
    }

    public string GetWeaponIconName()
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[WeaponDataSO] 무기 ID {id}의 key가 비어있습니다!");
            return "Weapon_Icon_Default"; // 빈 하얀색 대신 띄워줄 기본 아이콘 이름
        }

        // 2. 형식이 안 맞을 때 (언더바가 없을 때) 방어
        string[] splitData = key.Split('_');
        if (splitData.Length < 2)
        {
            Debug.LogWarning($"[WeaponDataSO] 무기 key 형식이 잘못되었습니다. (현재: {key}, 예상: wpn_Name)");
            return "Weapon_Icon_Default";
        }

        // 3. 문자열 보간을 사용하여 깔끔하게 조합
        string characterName = splitData[1];
        return $"Weapon_Icon_{characterName}";
    }
}
