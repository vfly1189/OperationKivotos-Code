/// <summary>
/// 입력 컨텍스트 — "지금 어떤 조작 입력을 받아야 하는가"의 상황.
/// Unity의 Action Map 개념에 대응(Phase 4에서 실제 InputActionAsset으로 이관 가능).
/// 새 상황(대화·컷신 등)이 실제로 필요해질 때 값을 추가한다(YAGNI).
/// </summary>
public enum InputContext
{
    Gameplay,   // 필드 조작 (기본) — 이동/평타/스킬/스왑/상호작용
    UI,         // 팝업·메뉴 열림 — 게임플레이 입력 차단
}
