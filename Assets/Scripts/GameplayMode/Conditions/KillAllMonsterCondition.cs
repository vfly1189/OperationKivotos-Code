using UnityEngine;

// [현재 방식] 몬스터 전멸 조건
public class KillAllMonstersCondition : BaseClearCondition
{
    private int _remainingMonsters;
    private MonsterController[] _monsters; // 캐싱용 변수 추가

    public override void SetupCondition(GameObject mapRoot)
    {
        _monsters = mapRoot.GetComponentsInChildren<MonsterController>(true);
        _remainingMonsters = _monsters.Length;

        foreach (var monster in _monsters)
        {
            // 방어 코드: 기존 구독을 먼저 빼고 추가
            monster.Stat.OnDead -= OnMonsterDead;
            monster.Stat.OnDead += OnMonsterDead;
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

    // [추가] 오브젝트 파괴 시 구독 해제 (메모리 누수 방지)
    private void OnDestroy()
    {
        if (_monsters != null)
        {
            foreach (var monster in _monsters)
            {
                if (monster != null && monster.Stat != null)
                    monster.Stat.OnDead -= OnMonsterDead;
            }
        }
    }
}