using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class SerikaCharacter : BaseCharacter
{
    public override void Init()
    {
        base.Init();
    }

    // 평타는 BaseCharacter 기본(UseAbility(Attack))으로 이관됨. 세리카는 컷신 종료 처리만 고유.
    protected override void OnCutsceneEnded(PlayableDirector director)
    {
        GameLog.Log("세리카 전용 컷신 종료 -> 일반상태로 전환");
        if (_gameCanvas != null) _gameCanvas.SetActive(true);
        IsUsingSkill = false;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
    }
}
