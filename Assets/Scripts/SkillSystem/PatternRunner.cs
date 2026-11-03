using UnityEngine;

// 패턴 실행기.
public class PatternRunner
{
    private Pattern _pattern;
    private PatternContext _ctx;
    private int _index;
    private bool _begun;

    private readonly PatternMemo _memo = new PatternMemo();   // 하나를 재사용 — 패턴 시작마다 비운다

    public bool IsBusy => _pattern != null;
    public Pattern Current => _pattern;

    public void Start(Pattern pattern, PatternContext ctx)
    {
        Cancel();

        if (pattern == null || pattern._steps.Count == 0) return;

        _memo.Clear();
        ctx.memo = _memo;

        _pattern = pattern;
        _ctx = ctx;
        _index = 0;
        _begun = false;
    }

    public NodeState Tick()
    {
        if (_pattern == null) return NodeState.Failure;

        PatternAction step = _pattern._steps[_index];

        if (!_begun) { step.Begin(_ctx); _begun = true; }   // 시작은 첫 Tick에서

        if (step.Tick() == NodeState.Running) return NodeState.Running;

        _index++; _begun = false;
        if (_index < _pattern._steps.Count) return NodeState.Running;   // 다음 동작은 다음 틱에

        _pattern = null;
        return NodeState.Success;

    } // Running이면 진행중, Success 면 끝남

    public void Cancel()
    {
        if (_pattern != null && _begun) _pattern._steps[_index].Cancel();
        _pattern = null;
    }

    // 신호(태그) → 현재 Action 자신, 그다음 Task 중 태그 · 조건이 맞는 것만 Fire. 목록 순서대로.
    public void OnEvent(GameplayTagSO tag)
    {
        if (_pattern == null || !_begun) return;

        PatternAction step = _pattern._steps[_index];
        step.OnTag(tag);

        foreach (PatternTask tsk in step._tasks)
        {
            if (tsk != null && tsk.Matches(tag, _ctx.memo))
                tsk.Fire(_ctx);
        }
    }
}
