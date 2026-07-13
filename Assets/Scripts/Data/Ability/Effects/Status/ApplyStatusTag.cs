using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 대상에 지속시간 GameplayTag를 부착한다(표식·가드·침묵 등 "상태 표식").
// ApplyStatModifier의 쌍둥이 — 값(modifier) 대신 태그를, 같은 ETargetScope로. 조합해서 쓴다:
//   브레이커 표식 = [ ApplyStatModifier(Target, Armor, −x), ApplyStatusTag(Target, State.Marked) ]
//   가드         = [ ApplyStatusTag(Self, State.Guarding), (선택) ApplyStatModifier(Self, Defense, +x) ]
// stateless → CreateRuntime() => this. 만료 타이머는 대상(또는 파티 컨테이너)의 StatusRunner가 소유.
[CreateAssetMenu(menuName = "Kivotos/Effect/ApplyStatusTag")]
public class ApplyStatusTag : EffectData, IEffect
{
    [Header("부착할 태그")]
    public GameplayTagSO tag;

    [Header("지속 / 대상")]
    public float duration = 8f;
    public ETargetScope scope = ETargetScope.Target;

    public override IEffect CreateRuntime() => this;

    public UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        if (tag == null) return UniTask.CompletedTask;

        foreach (var t in ResolveTargets(ctx))
        {
            GameplayTagOwner owner = GameplayTagOwner.EnsureOn(t.entity);
            StatusRunner runner = StatusRunner.EnsureOn(t.timerOwner);
            if (owner == null || runner == null) continue;

            runner.ApplyTimedTag(owner.Owned, tag, duration);
        }
        return UniTask.CompletedTask;
    }

    // (timerOwner = 만료 타이머를 소유할 GameObject, entity = 태그가 붙을 GameObject)
    private IEnumerable<(GameObject timerOwner, GameObject entity)> ResolveTargets(AbilityContext ctx)
    {
        switch (scope)
        {
            case ETargetScope.Self:
                if (ctx.CasterGO != null)
                    yield return (ctx.CasterGO, ctx.CasterGO);
                break;

            case ETargetScope.Target:
                if (ctx.Target != null)
                    yield return (ctx.Target, ctx.Target);
                break;

            case ETargetScope.Party:
                // 파티 스코프 태그는 영속 컨테이너에 부착(파티 단위 상태) — 스왑으로 멤버가 비활성돼도 만료된다.
                // 개별 멤버의 게이트가 읽어야 하는 태그라면 Self 스코프를 쓸 것.
                Transform container = Managers.Party?.GetCharacterContainer();
                if (container != null)
                    yield return (container.gameObject, container.gameObject);
                break;
        }
    }
}
