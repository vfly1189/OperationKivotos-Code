using UnityEngine;

// 역할이 부여하는 데이터. "캐릭터 = 개별데이터 + 역할데이터"의 역할 절반.
// E스킬을 캐릭터별 서브클래스가 아니라 역할 데이터로 부여한다(같은 역할끼리 공유 = DRY).
[CreateAssetMenu(fileName = "RoleData", menuName = "Data/RoleData")]
public class RoleDataSO : ScriptableObject
{
    public ERole role;

    [Header("역할이 부여하는 E스킬 (역할이 결정, 캐릭터 아님)")]
    public AbilityData eAbility;

    // 후속(Phase 2~3): 역할 소유 태그(Role.XXX), 역할 패시브 등
}
