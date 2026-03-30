using System;
using UnityEngine;

public class CharacterStateMachine
{ 
    public enum PlayerState
    {
        Idle,
        Move,
        Attack,
        Q_Skill_CutScene,
        Q_Skill,
        E_Skill,
        Death,
        Victory
    }


    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    public event Action<PlayerState> OnStateChanged;

    // 상태별 진입 불가 조건
    private readonly PlayerState[] _blockingStates =
    {
        PlayerState.Attack,
        PlayerState.Q_Skill_CutScene,
        PlayerState.Q_Skill,
        PlayerState.E_Skill,
        PlayerState.Death,
        PlayerState.Victory
    };

    public bool CanMove()
    {
        foreach (var state in _blockingStates)
        {
            if (CurrentState == state) return false;
        }
        return true;
    }

    public bool CanAttack()
    {
        //공격 할 수 없는 State가 아니면 true
        //밑에 작성된건 공격할 수 없는 상태

        return CurrentState != PlayerState.Attack &&
               CurrentState != PlayerState.Death &&
               CurrentState != PlayerState.Q_Skill_CutScene &&
               CurrentState != PlayerState.Q_Skill &&
               CurrentState != PlayerState.E_Skill &&
               CurrentState != PlayerState.Victory;
    }

    public bool CanUseSkill()
    {
        //스킬을 사용 할 수 없는 State가 아니면 true
        //밑에 작성된건 스킬을 사용할 수 없는 상태

        //여기는 좀 고민을 해봐야될듯?
        //공격 중에 스킬은 허용해도 될것같은데 ESkill 사용중이나 Victory나 이런상황에서는 못쓰는게 맞지 않나?

        return CurrentState != PlayerState.Attack &&
               CurrentState != PlayerState.Death &&
               CurrentState != PlayerState.Q_Skill &&
               CurrentState != PlayerState.E_Skill &&
               CurrentState != PlayerState.Victory;
    }

    public bool IsDead()
    {
        return CurrentState == PlayerState.Death;
    }

    public void ChangeState(PlayerState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }
}
