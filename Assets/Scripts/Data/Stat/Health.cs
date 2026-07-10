using System;
using UnityEngine;

// StatRefactor 3단계 — HP/피격/죽음/무적 상태를 BaseStat(God 컴포넌트)에서 분리한다.
// [Step 1] HP 숫자·사망 플래그·HP/사망 이벤트 소유.
// [Step 2] 방어자측 피격 파이프라인(TakeDamage)·무적·데미지 표시 이벤트·사망 발행을 통합.
//          Character/MonsterStat가 제각각 갖던 TakeDamage/HandleDeath를 여기 단일 구현으로 흡수.
//          캐릭터/몬스터 죽음의 '차이'는 서브클래스가 아니라 OnDead/OnDied '구독자'가 흡수한다.
// [Step 4] IDamageable seam을 이 컴포넌트로 이관 — 투사체/Effect의 TryGetComponent<IDamageable>가 이제 Health로 해석된다.
//          (BaseStat/Character/MonsterStat는 더 이상 IDamageable 아님 → 이중구현 모호성 없음)
public class Health : MonoBehaviour, IDamageable
{
    // MaxHp/Defense 등 속성을 읽어오기 위한 스탯 컬렉션 참조.
    private BaseStat _stat;

    public float CurrentHp { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsInvincible { get; set; } = false;

    public event Action<float, float> OnHpChanged; // cur, max
    public event Action OnDead;
    // 사망 순간 공격자(GameObject)를 전달. 경제 지급·재브로드캐스트 등이 구독한다.
    // (기존 MonsterStat.HandleDeath가 하던 OnKilledByAttacker 경로를 대체)
    public event Action<GameObject> OnDied;
    // 데미지 적용 순간. DamageNumberPresenter가 구독해 토스트를 그린다. (UI 책임 분리)
    public event Action<DamageTaken> OnDamageTaken;

    // 풀·중복부착 안전. lazy(BaseStat.HealthComp)로도 보장되며, 컨트롤러가 명시적으로 EnsureOn해 구독한다.
    public static Health EnsureOn(GameObject go, BaseStat stat)
    {
        var h = go.GetComponent<Health>();
        if (h == null) h = go.AddComponent<Health>();
        h._stat = stat;
        return h;
    }

    private float MaxHpValue => (_stat != null && _stat.MaxHp != null) ? _stat.MaxHp.Value : 0f;
    private float DefenseValue => (_stat != null && _stat.Defense != null) ? _stat.Defense.Value : 0f;

    // ── 방어자측 피격 파이프라인 (구 Character/MonsterStat.TakeDamage 통합) ──────────
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsInvincible || IsDead) return;

        float finalDamage = Mathf.Max(damageInfo.Amount - DefenseValue, 1);
        CurrentHp = Mathf.Clamp(CurrentHp - finalDamage, 0, MaxHpValue);

        // 데미지 표시 이벤트 (실제 토스트는 DamageNumberPresenter가 그림)
        OnDamageTaken?.Invoke(new DamageTaken(finalDamage, damageInfo.HitPoint, damageInfo.Attacker.layer, damageInfo.IsCritical));

        if (CurrentHp <= 0) Die(damageInfo.Attacker);

        OnHpChanged?.Invoke(CurrentHp, MaxHpValue);
    }

    private void Die(GameObject attacker)
    {
        if (IsDead) return; // 중복 호출 방지
        IsDead = true;
        OnDied?.Invoke(attacker); // 공격자 전달 (경제/재브로드캐스트)
        OnDead?.Invoke();         // 파라미터 없는 기존 사망 이벤트 (BaseCharacter 등)
    }

    // ── 상태 복원용 (CharacterStat의 ResetState/ReturnToTownState/SaveData 등이 사용) ──
    // HP를 raw로 대입(클램프는 호출부가 담당). 부활 시 사망 플래그 해제. 이벤트는 RaiseHpChanged로 별도 발행.
    public void SetHpRaw(float value) { CurrentHp = value; }
    public void SetDead(bool dead) { IsDead = dead; }
    public void RaiseHpChanged(float current, float max) { OnHpChanged?.Invoke(current, max); }

    // 풀에서 꺼낼 때 스탯은 유지하고 HP만 회복 + 사망 리셋. (원본 BaseStat.Recover 그대로)
    public void Recover()
    {
        IsDead = false;
        CurrentHp = MaxHpValue;
        OnHpChanged?.Invoke(CurrentHp, MaxHpValue);
    }

    // 원본 BaseStat.Heal 그대로.
    public void Heal()
    {
        CurrentHp = MaxHpValue;
        OnHpChanged?.Invoke(CurrentHp, MaxHpValue);
    }
}
