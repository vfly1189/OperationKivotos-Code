using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static BaseCharacter;

public class MonsterStat : BaseStat, IDamageable
{
    [Header("Data")]
    [SerializeField] private MonsterDataSO _data; // 초기 데이터
    // 몬스터 전용: 처치 시 주는 경험치, 드랍 아이템 확률 등
    public float DropExpAmount = 300f;

    private bool _isDead = false;
    public void Init(MonsterDataSO data, int stageLevel)
    {
        base.Init();
        // 스테이지 레벨에 따른 스탯 뻥튀기 적용
        float multiplier = 1 + (stageLevel * 0.1f);

        MaxHp.SetBaseValue(data.MaxHp * multiplier);
        Attack.SetBaseValue(data.Attack * multiplier);
        Defense.SetBaseValue(data.Defense * multiplier);

        CurrentHp = MaxHp.Value;
    }

    public override void Init()
    {
        base.Init();
        if (_data != null)
        {
            MaxHp.SetBaseValue(_data.MaxHp);
            Attack.SetBaseValue(_data.Attack);
            Defense.SetBaseValue(_data.Defense);
            CurrentHp = MaxHp.Value;
        }
    }

    public override void TakeDamage(DamageInfo damageInfo)
    {
        float finalDamage = Mathf.Max(damageInfo.Amount - Defense.Value, 1);
        CurrentHp -= finalDamage;
        CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp.Value);
        // 피격 이펙트, 사운드 처리 등을 damageInfo.HitPoint를 활용해 여기서 처리 가능

        if (CurrentHp <= 0 && _isDead == false)
        {
            Debug.Log("몬스터 사망 ");
            _isDead = true;
            HandleDeath(damageInfo.Attacker);
        }


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
                playerStat.AddExp(DropExpAmount); // 경험치 추가 함수 호출
                Debug.Log($"플레이어에게 경험치 {DropExpAmount} 지급!");
            }
        }

        // 2. 이벤트 발송 (나 죽었다!)
        CallOnDead();
    }
}
