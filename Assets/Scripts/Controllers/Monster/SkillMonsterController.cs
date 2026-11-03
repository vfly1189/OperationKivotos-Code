using System.Collections.Generic;
using UnityEngine;

// SS로 싸우는 일반몹. 무엇을 쓸지는 선택기(AmmoPatternSelector), 써도 되나 · 실행은 SS.
// 컨트롤러가 아는 건 "사거리 안이면 시도"와 행동 중 회전뿐 — 무기 · 탄 · 발사 방식은 전부 프리팹의 Pattern 에셋.
// (같은 SS를 보스는 HP선 · 가중치로, 일반몹은 탄 수로 부른다 — SS는 누가 부르는지 모른다)
public class SkillMonsterController : NormalMonsterController
{
    private AmmoPatternSelector _selector;

    protected override void Awake()
    {
        base.Awake();
        _selector = GetComponent<AmmoPatternSelector>();
    }

    public override void OnSpawn(SpawnContext ctx)
    {
        base.OnSpawn(ctx);
        _selector.ResetState();
    }

    protected override void OnReturnedHome() => _selector.ResetState();   // 리쉬 복귀 = 탄약도 원복

    protected override Node GetCombatNode()
    {
        return new Sequence(new List<Node>
        {
            new ActionNode(CheckAttackRange),
            new ActionNode(TryAttack)
        });
    }

    private NodeState CheckAttackRange() => IsTargetInAttackRange() ? NodeState.Success : NodeState.Failure;

    private NodeState TryAttack()
    {
        if (!_selector.TryPick(out Pattern pattern)) return NodeState.Failure;
        if (!_ss.TryUseSkill(pattern, BuildContext())) return NodeState.Failure;

        OnActionStarted();
        return NodeState.Running;
    }
}
