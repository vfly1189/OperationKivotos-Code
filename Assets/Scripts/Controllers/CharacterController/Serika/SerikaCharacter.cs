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

    //세리카는 한발씩 애니메이션 이벤트
    // 공격 상태일 때 매 프레임 실행될 로직
    protected override void PerformAttackAction()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

        FireOneBullet();
    }

    protected override void OnCutsceneEnded(PlayableDirector director)
    {
        Debug.Log("세리카 전용 컷신 종료 -> 일반상태로 전환");
        if (_gameCanvas != null) _gameCanvas.SetActive(true);
        IsUsingSkill = false;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
    }
}
