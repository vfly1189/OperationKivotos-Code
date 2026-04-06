using System;
using UnityEngine;

public class CharacterStateMachine
{
    public enum PlayerState
    {
        Idle, Move, Attack,
        QSkillCutScene, Q_Skill, E_Skill,
        Death, Victory
    }

    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
    public event Action<PlayerState> OnStateChanged;

    // 허용 상태를 명시적으로 나열 → blockingStates 배열 반복보다 빠르고 읽기 좋음
    public bool CanMove => CurrentState == PlayerState.Idle || CurrentState == PlayerState.Move;
    public bool CanSwap => CurrentState == PlayerState.Idle || CurrentState == PlayerState.Move;  // 공격·스킬 중 스왑 불가
    public bool CanAttack => CurrentState == PlayerState.Idle || CurrentState == PlayerState.Move;
    public bool CanUseSkill => CurrentState == PlayerState.Idle
                            || CurrentState == PlayerState.Move
                            || CurrentState == PlayerState.Attack; // 공격 중 스킬로 캔슬 허용

    public bool IsDead => CurrentState == PlayerState.Death;

    public void ChangeState(PlayerState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }
}