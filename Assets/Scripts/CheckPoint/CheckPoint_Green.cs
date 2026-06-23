using UnityEngine;

public class CheckPoint_Green : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        
    }

    public void OnTargetEnter(BaseCharacter character)
    {
        // 들어온 캐릭터의 힐링 오라 켜기
        character.PlayHealingAura().Forget();
        GameLog.Log("체크포인트 도달! 체력 회복 효과 시작");
    }

    public void OnTargetExit(BaseCharacter character)
    {

    }
}
