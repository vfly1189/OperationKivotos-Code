using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 대상 스탯에 지속시간 modifier(버프/방깎)를 적용한다.
// 버프(파티 공격력↑)·가드(시전자 방어력↑)·방깎(대상 방어력↓)을 값·부호·대상만 바꿔 재사용한다.
// stateless → CreateRuntime() => this. 지속 상태(타이머)는 대상의 StatusRunner가 소유.
[CreateAssetMenu(menuName = "Kivotos/Effect/ApplyStatModifier")]
public class ApplyStatModifier : EffectData, IEffect
{
    [Header("어느 스탯")]
    public EStatType stat = EStatType.Attack_Flat;

    [Header("값 / 연산 (버프 +, 방깎 -)")]
    public float value = 0.3f;                        // 예: PercentAdd 0.3 = +30%
    public StatModType modType = StatModType.PercentAdd;

    [Header("지속 / 대상")]
    public float duration = 8f;
    public ETargetScope scope = ETargetScope.Party;

    public override IEffect CreateRuntime() => this;

    public UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        foreach (var t in ResolveTargets(ctx))
        {
            StatusRunner runner = StatusRunner.EnsureOn(t.owner);
            Stat targetStat = t.statComp != null ? t.statComp.GetStat(stat) : null;
            if (runner == null || targetStat == null) continue;

            // 대상마다 새 modifier 생성 (source = 이 Effect). 공유 참조 금지 — 개별 해제를 위해.
            runner.ApplyTimed(targetStat, new StatModifier(value, modType, this), duration);
        }
        return UniTask.CompletedTask;
    }

    // (owner = 타이머를 소유할 GameObject, statComp = modifier를 걸 스탯)
    private IEnumerable<(GameObject owner, BaseStat statComp)> ResolveTargets(AbilityContext ctx)
    {
        switch (scope)
        {
            case ETargetScope.Self:
                if (ctx.CasterGO != null && ctx.CasterStat != null)
                    yield return (ctx.CasterGO, ctx.CasterStat);
                break;

            case ETargetScope.Target:
                if (ctx.Target != null && ctx.Target.TryGetComponent<BaseStat>(out var ts))
                    yield return (ctx.Target, ts);
                break;

            case ETargetScope.Party:
                // 스왑 시 멤버가 SetActive(false)되므로, 타이머는 영속 컨테이너가 소유해야 만료된다.
                List<BaseCharacter> members = Managers.Party?.GetMember();
                Transform container = Managers.Party?.GetCharacterContainer();
                if (members != null && container != null)
                {
                    foreach (BaseCharacter m in members)
                    {
                        if (m == null || m.Stat == null || m.Stat.IsDead) continue;
                        yield return (container.gameObject, m.Stat);
                    }
                }
                break;
        }
    }
}
