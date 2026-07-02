using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;


[CreateAssetMenu(menuName = "Kivotos/Effect/DealDamage")]
public class DealDamage : EffectData, IEffect
{
    public override IEffect CreateRuntime() => this;
    public UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        if (ctx.Target != null && ctx.Target.TryGetComponent<IDamageable>(out var target))
        {
            float amount = ctx.CasterStat.Attack.Value;
            target.TakeDamage(new DamageInfo(amount, ctx.CasterGO, false));
        }
        return UniTask.CompletedTask;                     // 즉시 완료
    }
}