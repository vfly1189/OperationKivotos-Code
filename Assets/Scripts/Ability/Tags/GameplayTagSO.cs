using UnityEngine;

// 게임플레이 태그 = 에셋 한 장. 참조 동등성으로 비교(멀티셋 키)하므로 문자열 오타·해시 충돌이 없고,
// 인스펙터에서 RequiredTags 등에 드래그로 꽂을 수 있다(EffectData/AbilityData SO 테마와 일관).
// 계층(State.Debuff.Stun)은 parent로 표현하되, 초기엔 사용하지 않는다(플랫 운용). 계층 매칭 확장 시 활성화.
[CreateAssetMenu(fileName = "Tag_", menuName = "Ability/GameplayTag")]
public class GameplayTagSO : ScriptableObject
{
    [Tooltip("계층 매칭용 상위 태그. 지금은 예약 필드(플랫 운용).")]
    public GameplayTagSO parent;

    // 로그·인스펙터 가독용 (기본 = 에셋 이름).
    public override string ToString() => name;
}
