using UnityEngine;

// 애니메이션을 직접 틀지 않는 동작 — 신호만 받는다.
// 플레이어는 애니를 상태 머신이 틀고(공격 · Q · E 상태), 패턴은 그 상태 동안 들어오는 신호(애니 비트)에만 반응한다.
//  - _fireOnBegin : 시작하자마자 이 태그로 자기 Task를 한 번 실행 (즉발 E)
//  - _holdUntilCancel : 켜면 끝나지 않고 기다린다 → 상태가 바뀔 때 시전자가 SkillSystem.Cancel로 끝낸다 (평타 · Q)
public class SignalAction : PatternAction
{
    [Tooltip("비우면 없음. 적으면 시작하는 순간 이 태그로 자기 Task를 실행")]
    [SerializeField] private GameplayTagSO _fireOnBegin;

    [Tooltip("켜면 시전자가 끊을 때까지 계속 신호를 받는다. 끄면 시작하고 바로 끝")]
    [SerializeField] private bool _holdUntilCancel = true;

    public override void Begin(PatternContext ctx)
    {
        if (_fireOnBegin == null || _tasks == null) return;

        foreach (PatternTask task in _tasks)
            if (task != null && task.Matches(_fireOnBegin, ctx.memo)) task.Fire(ctx);
    }

    public override NodeState Tick() => _holdUntilCancel ? NodeState.Running : NodeState.Success;

    public override void Cancel() { }
}
