using Cysharp.Threading.Tasks;
using System;

using UnityEngine;


public class MonsterStat : BaseStat, IDamageable
{
    private bool _isDead = false;

    public event Action<MonsterController> OnMonsterDead;
    private MonsterController _controller;

    // 추가된 변수: 보상 캐싱
    public int Level { get; private set; }
    public int FinalExpReward { get; private set; }
    public int FinalCreditReward { get; private set; }
    public int DropTableID { get; private set; }
    public MonsterDefine.MonsterSpawnType SpawnType { get; private set; }

    public void Init(MonsterBaseData baseData, MonsterLevelByStat levelStat)
    {
        base.Init();
        _isDead = false;

        if (_controller == null)
            _controller = GetComponent<MonsterController>();

        // 1. 배율을 적용하여 스탯 초기화
        float maxHp = baseData.BaseMaxHP * levelStat.MaxHPRate;
        float attack = baseData.BaseAttack * levelStat.AttackRate;
        float defense = baseData.BaseDefense * levelStat.DefenseRate;

        MaxHp.SetBaseValue(maxHp);
        Attack.SetBaseValue(attack);
        Defense.SetBaseValue(defense);
        Level = levelStat.Level;

        // 2. 보상 스케일링 적용 및 캐싱
        FinalExpReward = Mathf.RoundToInt(baseData.ExpReward); // 필요시 ExpRate 별도 추가
        FinalCreditReward = Mathf.RoundToInt(baseData.CreditReward * levelStat.CreditRate);
        DropTableID = baseData.DropTableID;
        SpawnType = baseData.SpawnType;

        CurrentHp = MaxHp.Value;
        CallOnHpChanged(CurrentHp, MaxHp.Value);
        ClearDeadEvent();

        OnMonsterDead = null;
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

        ToastDamageUI(finalDamage, damageInfo.HitPoint, true);
        CallOnHpChanged(CurrentHp, MaxHp.Value);
    }

    protected override void HandleDeath(GameObject shooter)
    {
        //공격자가 있고, 플레이어라면 경험치 지급
        if (shooter != null && shooter.CompareTag("Player"))
        {
            // 플레이어 스탯 컴포넌트 가져오기 (예: PlayerStat)
            CharacterStat playerStat = shooter.GetComponent<CharacterStat>();
            if (playerStat != null)
            {
                //playerStat.AddExp(DropExpAmount); // 경험치 추가 함수 호출
                Managers.Party.AddExp(FinalExpReward);
                Managers.Wallet.AddCurrency(CurrencyType.Credit, FinalCreditReward);

                if (FinalExpReward > 0)
                {
                    //Managers.UI.ShowGainExp(FinalExpReward);
                    UI_LootNotification.ShowGainExp(FinalExpReward).Forget();
                }


                if (FinalCreditReward > 0)
                {
                    //Managers.UI.ShowGainCredit(FinalCreditReward);
                    UI_LootNotification.ShowGainCredit(FinalCreditReward).Forget();
                }

                //Debug.Log($"플레이어에게 경험치 {DropExpAmount} 지급!");

                if (DropTableID >= 0)
                    Managers.Drop.RollAndGiveDropItems(DropTableID);
                
            }
        }

        // 2. 이벤트 발송 (나 죽었다!)
        CallOnDead();

        OnMonsterDead?.Invoke(_controller);
    }
}
