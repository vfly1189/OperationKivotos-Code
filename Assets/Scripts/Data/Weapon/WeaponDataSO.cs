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
}
