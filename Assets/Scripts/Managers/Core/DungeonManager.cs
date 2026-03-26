using System;
using UnityEngine;

public class DungeonManager
{
    public enum DungeonState { None, Playing, Cleared, Failed }
    public DungeonState CurrentState { get; private set; }

    public event Action OnDungeonCleared;
    private BaseClearCondition _currentCondition; // 현재 조건 캐싱

    public void StartDungeon(BaseClearCondition clearCondition)
    {
        CurrentState = DungeonState.Playing;

        // 이전 던전의 조건이 혹시 남아있다면 해제
        if (_currentCondition != null)
            _currentCondition.OnConditionMet -= HandleDungeonClear;

        _currentCondition = clearCondition;
        _currentCondition.OnConditionMet += HandleDungeonClear;
    }

    private void HandleDungeonClear()
    {
        if (CurrentState != DungeonState.Playing) return;
        CurrentState = DungeonState.Cleared;

        Managers.Party.FinishGame(true);
        OnDungeonCleared?.Invoke();
    }

    // [추가] 씬 전환 시 찌꺼기 청소용
    public void ClearDungeonData()
    {
        if (_currentCondition != null)
        {
            _currentCondition.OnConditionMet -= HandleDungeonClear;
            _currentCondition = null;
        }

        // OnDungeonCleared 구독자도 모두 날려버리기 (선택사항이나 안전함)
        OnDungeonCleared = null;

        CurrentState = DungeonState.None;
    }
}