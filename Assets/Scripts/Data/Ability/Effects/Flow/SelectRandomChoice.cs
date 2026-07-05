using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 선택: N개 중 랜덤으로 골라 캐스터에게 적용 요청. 렐릭이 뭔지 모름.
[CreateAssetMenu(menuName = "Kivotos/Effect/SelectRandomChoice")]
public class SelectRandomChoice : EffectData, IEffect
{
    public override IEffect CreateRuntime() => this;
    public UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        if (ctx.CasterGO.TryGetComponent<IChoiceHandler>(out var h) && h.ChoiceCount > 0)
            h.ApplyChoice(UnityEngine.Random.Range(0, h.ChoiceCount));
        return UniTask.CompletedTask;
    }
}