using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static MonsterController;

public abstract class RangedMonsterController : NormalMonsterController
{
    [Header("Ranged Settings")]
    [SerializeField] protected int _maxAmmo = 3; // 인스펙터에서 AR=3, RL=1, Tank=3 설정
    protected int _currentAmmo;


    protected override void OnEnable()
    {
        base.OnEnable();
        _currentAmmo = _maxAmmo;

        EnableAgentDelayAsync(_monsterCts.Token).Forget();
    }

    // 부모(Normal)가 비워둔 전투 노드를 장전 시스템으로 조립해서 반환
    protected override Node GetCombatNode()
    {
        return new Sequence(new List<Node>
        {
            new ActionNode(CheckAttackRange),
            new ActionNode(HandleCombat) // 탄창 검사 후 공격 or 장전 분기
        });
    }


    private async UniTaskVoid EnableAgentDelayAsync(CancellationToken token)
    {
        // 1프레임 대기 (파괴/비활성화 시 즉시 취소되도록 토큰 연동)
        bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        if (isCanceled) return;

        if (_state != MonsterState.Dead && _agent != null)
        {
            _agent.enabled = true;
        }

        _state = MonsterState.Idle;
    }

    private NodeState CheckAttackRange()
    {
        // 공격 중이거나 장전 중이면 거리가 멀어져도 동작을 마칠 수 있도록 유지
        if (_state == MonsterState.Attacking || _state == MonsterState.Reloading)
            return NodeState.Success;

        if (_target == null) return NodeState.Failure;
        float distance = Vector3.Distance(transform.position, _target.position);

        return (distance <= _attackRange) ? NodeState.Success : NodeState.Failure;
    }

    private NodeState HandleCombat()
    {
        if (_state == MonsterState.Reloading || _state == MonsterState.Attacking)
        {
            if (_state == MonsterState.Attacking) RotateToTarget();
            return NodeState.Running;
        }

        if (_currentAmmo <= 0)
        {
            StartReload();
            return NodeState.Running;
        }

        StartAttack();
        return NodeState.Running;
    }

    private void StartAttack()
    {
        _state = MonsterState.Attacking;
        if (_agent != null && _agent.enabled) _agent.isStopped = true;
        _anim.CrossFade("Attack_Start", 0.1f);
        _currentAmmo--;
    }

    private void StartReload()
    {
        _state = MonsterState.Reloading;
        if (_agent != null && _agent.enabled) _agent.isStopped = true;
        _anim.CrossFade("Reload", 0.1f);
    }

    // 애니메이션 종료 이벤트들
    public void OnAttackFinished() { if (_state != MonsterState.Dead) _state = MonsterState.Idle; }
    public void OnReloadFinished() { _currentAmmo = _maxAmmo; if (_state != MonsterState.Dead) _state = MonsterState.Idle; }
}