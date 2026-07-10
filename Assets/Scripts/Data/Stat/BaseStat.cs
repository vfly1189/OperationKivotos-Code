using System;
using System.Collections.Generic;
using UnityEngine;

// 데미지가 적용된 순간의 표시용 데이터. Health가 이 데이터를 발행하고, 실제 UI는 DamageNumberPresenter가 그린다.
public readonly struct DamageTaken
{
    public readonly float Amount;
    public readonly Vector3 HitPoint;
    public readonly LayerMask AttackerLayer;
    public readonly bool IsCritical;

    public DamageTaken(float amount, Vector3 hitPoint, LayerMask attackerLayer, bool isCritical)
    {
        Amount = amount;
        HitPoint = hitPoint;
        AttackerLayer = attackerLayer;
        IsCritical = isCritical;
    }
}

// 속성(Attribute) 저장·합산만 담당하는 스탯 컬렉션. = GAS의 AttributeSet.
// HP/피격/죽음/무적은 Health 컴포넌트가 소유한다(StatRefactor 3단계 완료). BaseStat은 HP 상태를 노출하지 않는다.
// (외부에서 HP/사망이 필요하면 HealthComp를 통해 Health를 구독/조회한다.)
public class BaseStat : MonoBehaviour
{
    protected Dictionary<EStatType, Stat> _stats = new Dictionary<EStatType, Stat>();

    // 공통 스탯 (HP, 공격력, 방어력)
    // 외부용 껍데기 프로퍼티 (Flat 키를 대표 키로 사용)
    public Stat MaxHp => GetStat(EStatType.MaxHP_Flat);
    public Stat Attack => GetStat(EStatType.Attack_Flat);
    public Stat Defense => GetStat(EStatType.Defense_Flat);

    // HP/사망/무적 상태를 소유하는 Health 컴포넌트로의 접근점.
    // 자기부트스트랩 — 접근 시점에 없으면 만든다(부착 타이밍에 무관하게 NRE 차단). Health가 MaxHp/Defense를 이 Stat에서 읽는다.
    protected Health _health;
    public Health HealthComp
    {
        get
        {
            if (_health == null) _health = Health.EnsureOn(gameObject, this);
            return _health;
        }
    }

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

    // 스탯 값 변동(모디파이어 추가/해제·기본값 변경) 구독. UI 실시간 갱신용.
    public void SubscribeStatChanged(Action cb)
    {
        if (cb == null) return;
        foreach (var s in _stats.Values) s.OnChanged += cb;
    }

    public void UnsubscribeStatChanged(Action cb)
    {
        if (cb == null) return;
        foreach (var s in _stats.Values) s.OnChanged -= cb;
    }

    // 공격 시 내보낼 데미지 패킷을 조립한다. (발사 경로 공용 진입점)
    // 기본은 크리 없음 — 몬스터가 이 구현을 그대로 사용한다.
    // 크리를 굴리는 캐릭터는 CharacterStat에서 override.
    public virtual DamageInfo BuildOutgoingDamage(GameObject attacker)
        => new DamageInfo(Attack.Value, attacker, false);
}
