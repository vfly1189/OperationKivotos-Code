using UnityEngine;

// 애니메이터 상태 하나를 틀고 끝날 때까지 기다린다.
// 끝 = 기본은 "그 상태가 끝나거나 빠져나감". 상태가 이어지는 애니(Attack_Start → Ing → End)는
// 첫 전이에서 끝나 버리므로 _endTag를 준다 → 그 태그(애니 이벤트)가 올 때까지 기다린다.
public class AnimationAction : PatternAction
{
    [SerializeField] private string _state;
    [SerializeField] private float _fade = 0.1f;

    [Tooltip("비우면 상태가 끝날 때 끝. 적으면 이 태그가 올 때 끝 (이어지는 애니의 마지막 클립 이벤트)")]
    [SerializeField] private GameplayTagSO _endTag;

    private int _hash;
    private Animator _animator;
    private int _beginFrame;
    private bool _ended;

    private void Awake() { _hash = Animator.StringToHash(_state); }

    public override void Begin(PatternContext ctx)
    {
        _animator = ctx.owner.GetComponent<Animator>();
        _animator.CrossFade(_state, _fade);

        _beginFrame = Time.frameCount;
        _ended = false;
    }

    public override NodeState Tick()
    {
        if (_endTag != null) return _ended ? NodeState.Success : NodeState.Running;

        // CrossFade는 다음 Animator 갱신에 반영 → Begin과 같은 프레임엔 아직 이전 상태
        if (Time.frameCount == _beginFrame) return NodeState.Running;
        if (_animator.IsInTransition(0)) return NodeState.Running;

        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash != _hash) return NodeState.Success;   // 자동 전이로 이미 빠져나감
        return info.normalizedTime >= 1f ? NodeState.Success : NodeState.Running;
    }

    public override void OnTag(GameplayTagSO tag)
    {
        if (_endTag != null && tag == _endTag) _ended = true;
    }

    public override void Cancel()
    {
        _animator = null;
    }
}
