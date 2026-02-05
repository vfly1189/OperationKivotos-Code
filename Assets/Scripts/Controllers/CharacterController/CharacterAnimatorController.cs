using UnityEngine;

public class CharacterAnimationController
{
    private Animator _animator;

    public CharacterAnimationController(Animator animator)
    {
        _animator = animator;
    }

    public void PlayState(CharacterStateMachine.PlayerState state)
    {
        switch (state)
        {
            case CharacterStateMachine.PlayerState.Idle:
                _animator.CrossFade("Idle", 0.3f);
                break;
            case CharacterStateMachine.PlayerState.Move:
                _animator.CrossFade("Move", 0.1f);
                break;
            case CharacterStateMachine.PlayerState.Attack:
                _animator.CrossFade("Attack_Ing", 0.0f);
                break;
            case CharacterStateMachine.PlayerState.Q_Skill_CutScene:
                _animator.CrossFade("Q_Skill_CutScene", 0.0f);
                break;
            case CharacterStateMachine.PlayerState.Q_Skill:
                _animator.CrossFade("Q_Skill", 0.0f);
                break;
            case CharacterStateMachine.PlayerState.E_Skill:
                _animator.CrossFade("E_Skill", 0.0f);
                break;
            case CharacterStateMachine.PlayerState.Death:
                _animator.CrossFade("Death", 0.1f);
                break;
            case CharacterStateMachine.PlayerState.Victory:
                _animator.CrossFade("Victory_Start", 0.0f);
                break;
        }
    }
}
