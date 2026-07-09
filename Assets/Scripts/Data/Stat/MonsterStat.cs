using Cysharp.Threading.Tasks;
using System;

using UnityEngine;


public class MonsterStat : BaseStat, IDamageable
{
    private bool _isDead = false;

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

        // 스탯 세팅이 끝나면 HP를 최대로 채움
        Recover();
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

    // 풀 재사용 시 사망 가드를 반드시 리셋한다. (OnEnable·SetStat 양쪽 경로에서 Recover 호출됨)
    // 이 리셋이 없으면 2회차 생애에서 아래 TakeDamage의 사망 가드가 막혀 HandleDeath(→보상)가 안 불린다.
    public override void Recover()
    {
        base.Recover();
        _isDead = false;
    }

    public override void TakeDamage(DamageInfo damageInfo)
    {
        float finalDamage = Mathf.Max(damageInfo.Amount - Defense.Value, 1);
        CurrentHp -= finalDamage;
        CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp.Value);

        if (CurrentHp <= 0 && _isDead == false)
        {
            _isDead = true;
            HandleDeath(damageInfo.Attacker);
        }

        RaiseDamageTaken(finalDamage, damageInfo.HitPoint, damageInfo.Attacker.layer, damageInfo.IsCritical);
        CallOnHpChanged(CurrentHp, MaxHp.Value);
    }

    protected override void HandleDeath(GameObject shooter)
    {
        // 보상(경험치·크레딧·드랍/알림UI) 지급은 MonsterReward가 구독해서 처리한다. (경제 로직 분리)
        OnKilledByAttacker?.Invoke(shooter);

        // 이벤트 발송 (나 죽었다!)
        CallOnDead();

        OnMonsterDead?.Invoke(_controller);
    }
}
