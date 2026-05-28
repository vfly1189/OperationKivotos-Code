using UnityEngine;

[System.Serializable]
public class CharacterRuntimeData
{
    public int id;           // 캐릭터 식별자
    public int level;           // 현재 레벨
    public float currentExp;    // 현재 경험치
    // public float currentHp;  // (선택) 던전 실패 후 체력도 유지할지? 보통은 풀피로 리셋

    public int weaponLevel;
}
