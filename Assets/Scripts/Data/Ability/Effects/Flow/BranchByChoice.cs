using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 분기: 캐스터의 현재 선택으로 자식 하나 실행. 역시 렐릭 모름.
[CreateAssetMenu(menuName = "Kivotos/Effect/BranchByChoice")]
public class BranchByChoice : EffectData, IEffect
{
    [SerializeField] private List<EffectData> _branches = new();
    public override IEffect CreateRuntime() => this;
    public async UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        if (!ctx.CasterGO.TryGetComponent<IChoiceHandler>(out var h)) return;
        int i = h.CurrentChoice;
        if (i < 0 || i >= _branches.Count || _branches[i] == null) return;
        await _branches[i].CreateRuntime().ExecuteAsync(ctx, token);

        h.ClearChoice();
    }
}