using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BaseStat : MonoBehaviour, IDamageable
{
    protected Dictionary<EStatType, Stat> _stats = new Dictionary<EStatType, Stat>();

    // 공통 스탯 (HP, 공격력, 방어력)
    // 외부용 껍데기 프로퍼티 (Flat 키를 대표 키로 사용)
    public Stat MaxHp => GetStat(EStatType.MaxHP_Flat);
    public Stat Attack => GetStat(EStatType.Attack_Flat);
    public Stat Defense => GetStat(EStatType.Defense_Flat);


    public float CurrentHp { get; protected set; }
    //public bool IsDead => CurrentHp <= 0;
    public bool IsDead { get; protected set; }
    // 사망 이벤트는 공통
    public event Action OnDead;
    public event Action<float, float> OnHpChanged;

    public virtual void Init()
    {
        // 딕셔너리에는 대표 키(_Flat)로 하나씩만 생성합니다. (_Percent 키는 만들지 않음!)
        _stats[EStatType.MaxHP_Flat] = new Stat();
        _stats[EStatType.Attack_Flat] = new Stat();
        _stats[EStatType.Defense_Flat] = new Stat();
    }

    //스탯 매핑
    public Stat GetStat(EStatType type)
    {
        EStatType targetKey = type;

        // 퍼센트 계열의 Enum이 들어오면, 원본(Flat) 스탯 객체를 바라보도록 키를 변경합니다.
        switch (type)
        {
            case EStatType.MaxHP_Percent: targetKey = EStatType.MaxHP_Flat; break;
            case EStatType.Attack_Percent: targetKey = EStatType.Attack_Flat; break;
            case EStatType.Defense_Percent: targetKey = EStatType.Defense_Flat; break;
        }

        if (_stats.TryGetValue(targetKey, out Stat stat))
        {
            return stat;
        }

        return null;
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
    
    protected async void ToastDamageUI(float damage, Vector3 hitPoint, bool isCritical)
    {
        UI_DamageToast toast = await Managers.UI.MakeSubItemAsync<UI_DamageToast>("UI_DamageToast", Managers.UI.CanvasSystem.transform);
        toast.gameObject.SetActive(true);
        Camera mainCam = Camera.main;
        Vector3 screenPos = mainCam.WorldToScreenPoint(hitPoint);
        toast.transform.position = screenPos;
        if (toast != null)
        {
            // int로 형변환해서 넘겨줌
            toast.SetupDamageText((int)damage, isCritical);
        }
    }
}
