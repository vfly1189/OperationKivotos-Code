using Cysharp.Threading.Tasks;
using System;

using UnityEngine;


public class MonsterStat : BaseStat
{
    // 사망 감지·가드는 Health.IsDead로 통일(구 _isDead 제거). 풀 리셋도 Health.Recover가 담당.

    public event Action<BaseMonsterController> OnMonsterDead;

    // 사망 순간 공격자(GameObject)를 전달 — 보상 지급은 MonsterReward가 구독해서 처리한다. (경제 로직 분리)
    public event Action<GameObject> OnKilledByAttacker;

    private BaseMonsterController _controller;

    // 추가된 변수: 보상 캐싱
    public int Level { get; private set; }
    public int FinalExpReward { get; private set; }
    public int FinalCreditReward { get; private set; }
    public int DropTableID { get; private set; }
    public MonsterDefine.MonsterSpawnType SpawnType { get; private set; }

    public void SetStat(MonsterBaseData baseData, MonsterLevelByStat levelStat)
    {
        // [방어 코드 추가] 스탯 딕셔너리가 아직 안 만들어졌다면 먼저 만들어줍니다.
        if (_stats == null || _stats.Count == 0)
        {
            base.Init(); // BaseStat.Init()을 호출하여 딕셔너리 할당
        }

        if (_controller == null)
            _controller = GetComponent<BaseMonsterController>();

        // 1. 배율을 적용하여 스탯 세팅 (이제 MaxHp가 절대 null이 아님)
        MaxHp.SetBaseValue(baseData.BaseMaxHP * levelStat.MaxHPRate);
        Attack.SetBaseValue(baseData.BaseAttack * levelStat.AttackRate);
        Defense.SetBaseValue(baseData.BaseDefense * levelStat.DefenseRate);
        Level = levelStat.Level;

        FinalExpReward = Mathf.RoundToInt(baseData.ExpReward);
        FinalCreditReward = Mathf.RoundToInt(baseData.CreditReward * levelStat.CreditRate);
        DropTableID = baseData.DropTableID;
        SpawnType = baseData.SpawnType;

        OnMonsterDead = null;

        // Health의 사망 발행을 몬스터 고유 이벤트로 재브로드캐스트(경제=MonsterReward, 리스폰=MonsterSpawner).
        // idempotent(-=/+=) — 풀 재사용으로 SetStat이 여러 번 불려도 중복 구독 없음. Health는 GO에 영속.
        HealthComp.OnDied -= OnHealthDied;
        HealthComp.OnDied += OnHealthDied;

        // 스탯 세팅이 끝나면 HP를 최대로 채움 (사망 플래그 리셋 포함)
        HealthComp.Recover();
    }

    // Health가 사망을 감지·발행하면, 몬스터 고유 사망 이벤트로 되쏜다. (구 HandleDeath의 이벤트 발행부)
    private void OnHealthDied(GameObject attacker)
    {
        // 보상(경험치·크레딧·드랍/알림UI)은 MonsterReward가 OnKilledByAttacker를 구독해 처리.
        OnKilledByAttacker?.Invoke(attacker);
        // 리스폰은 MonsterSpawner가 OnMonsterDead를 구독해 처리.
        OnMonsterDead?.Invoke(_controller);
    }


    //public override void Init()
    //{
    //    base.Init();
    //    _isDead = false;

    //    if (_controller == null)
    //        _controller = GetComponent<MonsterController>();


    //    if (_data != null)
    //    {
    //        MaxHp.SetBaseValue(_data.MaxHp);
    //        Attack.SetBaseValue(_data.Attack);
    //        Defense.SetBaseValue(_data.Defense);
    //        CurrentHp = MaxHp.Value;
    //    }
    //    // 혹시 모르니 HP바 갱신 이벤트 한 번 쏴주기
    //    CallOnHpChanged(CurrentHp, MaxHp.Value);
    //    ClearDeadEvent();

    //    OnMonsterDead = null;
    //}

}
