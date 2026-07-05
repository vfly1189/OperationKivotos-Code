using UnityEngine;

public interface IChoiceHandler
{
    int ChoiceCount { get; }        // 보스가 선언: 선택지 수 (렐릭 = 2)
    void ApplyChoice(int index);    // 보스가 처리: 그 선택을 적용
    int CurrentChoice { get; }      // 나중에 분기가 읽음

    void ClearChoice();
}