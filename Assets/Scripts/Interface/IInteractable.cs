using UnityEngine;

public interface IInteractable
{
    void Interact();

    // [추가] 플레이어가 트리거 영역에 들어왔을 때/나갔을 때의 동작
    void OnTargetEnter(BaseCharacter character);
    void OnTargetExit(BaseCharacter character);
}
