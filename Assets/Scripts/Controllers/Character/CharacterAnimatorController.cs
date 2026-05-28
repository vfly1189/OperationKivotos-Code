
using UnityEngine;

public class CharacterAnimationController
{
    private readonly Animator _animator;

    public CharacterAnimationController(Animator animator)
    {
        this._animator = animator;
    }

    //public void PlayState(CharacterStateMachine.PlayerState state)
    //{
    //    if (!_animator.gameObject.activeInHierarchy) return; // 비활성 오브젝트 방어

    //    switch (state)
    //    {
    //        case CharacterStateMachine.PlayerState.Idle: _animator.CrossFade("Idle", 0.3f); break;
    //        case CharacterStateMachine.PlayerState.Move: _animator.CrossFade("Move", 0.1f); break;
    //        case CharacterStateMachine.PlayerState.Attack: _animator.CrossFade("Attack_Ing", 0.0f); break;
    //        case CharacterStateMachine.PlayerState.QSkillCutScene: _animator.CrossFade("Q_Skill_CutScene", 0.0f); break;
    //        case CharacterStateMachine.PlayerState.Q_Skill: _animator.CrossFade("Q_Skill", 0.0f); break;
    //        case CharacterStateMachine.PlayerState.E_Skill: _animator.CrossFade("E_Skill", 0.0f); break;
    //        case CharacterStateMachine.PlayerState.Death: _animator.CrossFade("Death", 0.1f); break;
    //        case CharacterStateMachine.PlayerState.Victory: _animator.CrossFade("Victory_Start", 0.0f); break;
    //    }
    //}

    public void PlayState(CharacterStateMachine.PlayerState state)
    {
        if (!_animator.gameObject.activeInHierarchy) return;

        switch (state)
        {
            case CharacterStateMachine.PlayerState.Idle: _animator.CrossFade("Idle", 0.3f); break;
            case CharacterStateMachine.PlayerState.Move: _animator.CrossFade("Move", 0.1f); break;

            // [수정됨] CrossFade 대신 Play 사용 (-1 레이어, 0f 시간부터 강제 재생)
            case CharacterStateMachine.PlayerState.Attack: _animator.Play("Attack_Ing", -1, 0f); break;

            case CharacterStateMachine.PlayerState.QSkillCutScene: _animator.CrossFade("Q_Skill_CutScene", 0.0f); break;
            case CharacterStateMachine.PlayerState.Q_Skill: _animator.CrossFade("Q_Skill", 0.0f); break;
            case CharacterStateMachine.PlayerState.E_Skill: _animator.CrossFade("E_Skill", 0.0f); break;
            case CharacterStateMachine.PlayerState.Death: _animator.CrossFade("Death", 0.1f); break;
            case CharacterStateMachine.PlayerState.Victory: _animator.CrossFade("Victory_Start", 0.0f); break;
        }
    }
}