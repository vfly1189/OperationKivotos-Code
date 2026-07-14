/// <summary>
/// 입력의 "의도(intent)". 물리 키(Key.E 등)가 아니라 게임적 의미를 나타낸다.
/// 소비자는 이 Intent만 알고, 실제 키는 InputManager가 _keyMap으로 관리한다.
/// (Phase 1: magic string 제거 — "E_Skill" 같은 문자열을 대체)
/// </summary>
public enum InputIntent
{
    None,

    // 전투 (추후 선입력 버퍼 대상)
    Attack,
    SkillE,
    SkillQ,

    // 시스템
    Interact,
    Info,
    Inventory,
    Escape,

    // 파티 스왑
    Swap1,
    Swap2,
    Swap3,
    Swap4,
}
