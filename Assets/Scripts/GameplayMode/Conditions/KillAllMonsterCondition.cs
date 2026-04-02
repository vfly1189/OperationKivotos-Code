using System.Collections.Generic;
using UnityEngine;

// [현재 방식] 몬스터 전멸 조건
public class KillAllMonstersCondition : BaseClearCondition
{
    private int _remainingMonsters;
    private List<BaseMonsterController> _monsters;

    // 맵을 뒤지는 대신, 스폰된 몬스터 리스트를 직접 넘겨받습니다.
    public void Setup(List<BaseMonsterController> monsters)
    {
        _monsters = monsters;
        _remainingMonsters = _monsters.Count;

        if (_remainingMonsters == 0)
        {
            InvokeConditionMet();
            return;
        }

        foreach (var monster in _monsters)
        {
            // 방어 코드: 기존 구독을 먼저 빼고 추가
            monster.OnDead -= OnMonsterDead;
            monster.OnDead += OnMonsterDead;
        }
    }

    private void OnMonsterDead()
    {
        _remainingMonsters--;
        if (_remainingMonsters <= 0)
        {
            InvokeConditionMet();
        }
    }

    private void OnDestroy()
    {
        if (_monsters != null)
        {
            foreach (var monster in _monsters)
            {
                if (monster != null)
                    monster.OnDead -= OnMonsterDead;
            }
        }
    }
}