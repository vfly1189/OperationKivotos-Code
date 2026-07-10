using UnityEngine;

// 시전자 신원 표식 (AbilityContext.Caster). 발동 진입점은 구현 타입마다 다르다:
//  - 캐릭터: UseAbility(CharacterAbilitySlot)
//  - 몬스터/보스: 각 컨트롤러의 고유 경로
public interface IAbilityCaster
{
}
