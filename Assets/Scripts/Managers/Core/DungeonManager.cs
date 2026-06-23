using System;
using System.Collections.Generic;
using UnityEngine;

public class DungeonManager
{
    public enum DungeonState { None, Playing, Cleared, Failed }
    public DungeonState CurrentState { get; private set; } = DungeonState.None;

    private List<BaseCondition> clearConditions = new List<BaseCondition>();
    private List<BaseCondition> failConditions = new List<BaseCondition>();

    public event Action OnDungeonCleared;
    public event Action OnDungeonFailed;


    public void StartDungeon()
    {
        CurrentState = DungeonState.Playing;
    }

    public void AddClearCondition(BaseCondition condition)
    {
        condition.OnConditionMet -= HandleClearMet; // 중복 구독 방지
        condition.OnConditionMet += HandleClearMet;
        clearConditions.Add(condition);
    }

    public void AddFailCondition(BaseCondition condition)
    {
        condition.OnConditionMet -= HandleFailMet; // 중복 구독 방지
        condition.OnConditionMet += HandleFailMet;
        failConditions.Add(condition);
    }

    private void HandleClearMet()
    {
        GameLog.Log($"[DungeonManager] 클리어 조건 달성됨! 현재 상태: {CurrentState}");

        // [핵심] 락(Lock) 처리
        if (CurrentState != DungeonState.Playing)
        {
            GameLog.LogWarning("[DungeonManager] 현재 상태가 Playing이 아니라서 클리어를 무시합니다!");
            return;
        }

        Managers.Party.SetPartyInvincible(true);

        CurrentState = DungeonState.Cleared;
        OnDungeonCleared?.Invoke();
    }

    private void HandleFailMet()
    {
        // [핵심] 락(Lock) 처리
        if (CurrentState != DungeonState.Playing) return;

        CurrentState = DungeonState.Failed;
        OnDungeonFailed?.Invoke();
    }

    // 씬 전환 시 완벽한 찌꺼기 청소
    public void ClearDungeonData()
    {
        // 2. 다중 조건 리스트 청소 및 구독 해제 (메모리 누수 방지)
        foreach (var condition in clearConditions)
        {
            if (condition != null) condition.OnConditionMet -= HandleClearMet;
        }
        clearConditions.Clear();

        foreach (var condition in failConditions)
        {
            if (condition != null) condition.OnConditionMet -= HandleFailMet;
        }
        failConditions.Clear();

        // 3. 외부 구독자 날리기
        OnDungeonCleared = null;
        OnDungeonFailed = null;

        // 4. 상태 초기화
        CurrentState = DungeonState.None;
    }
}