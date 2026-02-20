using System;
using UnityEngine;

public class BaseStat : MonoBehaviour, IDamageable
{
    // 공통 스탯 (HP, 공격력, 방어력)
    public Stat MaxHp;
    public Stat Attack;
    public Stat Defense;

    public float CurrentHp { get; protected set; }
    //public bool IsDead => CurrentHp <= 0;
    public bool IsDead { get; protected set; }
    // 사망 이벤트는 공통
    public event Action OnDead;
    public event Action<float, float> OnHpChanged;

    public virtual void Init()
    {
        // 스탯 클래스 초기화
        MaxHp = new Stat();
        Attack = new Stat();
        Defense = new Stat();


    }
    protected virtual void HandleDeath(GameObject shooter)
    {
        OnDead?.Invoke();
        // 실제 파괴나 비활성화는 Controller에서 이벤트 구독해서 처리하거나 여기서 구현
    }

    protected void CallOnHpChanged(float current, float max)
    {
        OnHpChanged?.Invoke(current, max);
    }

    protected void CallOnDead()
    {
        OnDead?.Invoke();
    }

    public virtual void TakeDamage(DamageInfo damageInfo) { }

    protected void ClearDeadEvent() { OnDead = null; }
}
