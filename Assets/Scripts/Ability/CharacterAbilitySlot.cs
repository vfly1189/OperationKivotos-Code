using System;

// 캐릭터 어빌리티 슬롯 키 (캐릭터 로컬 — 몬스터/보스는 사용하지 않음).
// 값은 애니메이션 이벤트의 intParameter와 1:1 대응하므로 고정한다.
public enum CharacterAbilitySlot
{
    Attack = 0,   // 평타
    Skill  = 1,   // Q
    Ex     = 2,   // E (역할에서 해석됨)
}

// 인스펙터에서 슬롯 ↔ 어빌리티를 명시적으로 엮는 쌍.
// (Ex는 역할에서 런타임 주입되므로 보통 Attack/Skill만 지정한다.)
[Serializable]
public struct AbilitySlotEntry
{
    public CharacterAbilitySlot Slot;
    public AbilityData Ability;
}
