using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MonsterController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _detectRange = 10f; // 감지 범위
    [SerializeField] private float _attackRange = 2f;  // 공격 범위
    [SerializeField] private float _moveSpeed = 3.5f;

    [Header("Components")]
    [SerializeField] private Animator _anim;
    [SerializeField] private NavMeshAgent _agent; // 길찾기용 (필수 아님, Transform이동이면 제거)

    // 스탯 (필수 재사용)
    public CharacterStat Stat { get; private set; }

    // 타겟(플레이어)
    private Transform _target;
    // 행동 트리 루트 노드
    private Node _topNode;

    private void Awake()
    {
        Stat = GetComponent<CharacterStat>();
        if (Stat != null) Stat.Init();

        if (_anim == null) _anim = GetComponent<Animator>();
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();

        // NavMeshAgent 설정
        if (_agent != null)
        {
            _agent.speed = _moveSpeed;
            _agent.stoppingDistance = _attackRange - 0.5f;
            _agent.updateRotation = true;
        }

        // 행동 트리 조립
        ConstructBehaviorTree();
    }

    private void Start()
    {
        // 플레이어 찾기 (태그나 매니저 활용)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _target = player.transform;
    }

    private void Update()
    {
        // 매 프레임 트리 평가
        if (_topNode != null)
            _topNode.Evaluate();

        // 사망 체크 등은 별도 이벤트로 처리하거나 여기서 체크
        if (Stat.CurrentHp <= 0)
        {
            // 사망 처리 로직...
        }
    }

    // ========================================================================
    // [행동 트리 조립] (핵심)
    // ========================================================================
    private void ConstructBehaviorTree()
    {
        // 1. 공격 노드 (Sequence: 사거리 내? -> 공격 실행)
        Node attackSequence = new Sequence(new List<Node>
        {
            new ActionNode(CheckAttackRange),
            new ActionNode(PerformAttack)
        });

        // 2. 추적 노드 (Sequence: 타겟 감지? -> 이동 실행)
        Node chaseSequence = new Sequence(new List<Node>
        {
            new ActionNode(CheckDetectRange),
            new ActionNode(TrackTarget)
        });

        // 3. 최상위 Selector (공격 우선, 안되면 추적, 안되면 대기)
        _topNode = new Selector(new List<Node>
        {
            attackSequence,
            chaseSequence,
            new ActionNode(Idle) // 아무것도 해당 안 되면 대기
        });
    }

    // ========================================================================
    // [Action Nodes] 실제 로직 함수들
    // ========================================================================

    // 거리 체크: 공격 범위 안인가?
    private NodeState CheckAttackRange()
    {
        if (_target == null) return NodeState.Failure;

        float distance = Vector3.Distance(transform.position, _target.position);
        return (distance <= _attackRange) ? NodeState.Success : NodeState.Failure;
    }

    // 공격 실행
    private NodeState PerformAttack()
    {
        // 쿨타임 체크 등 필요 시 추가

        _agent.isStopped = true; // 이동 멈춤
        _anim.SetBool("IsMoving", false);
        _anim.SetTrigger("Attack"); // 애니메이션 트리거

        // 회전 (공격 시 타겟 바라보기)
        Vector3 dir = (_target.position - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);

        return NodeState.Success;
        // 주의: 공격 애니메이션이 끝날 때까지 기다리려면 Running을 반환하는 별도 로직 필요
    }

    // 거리 체크: 감지 범위 안인가?
    private NodeState CheckDetectRange()
    {
        if (_target == null) return NodeState.Failure;

        float distance = Vector3.Distance(transform.position, _target.position);
        return (distance <= _detectRange) ? NodeState.Success : NodeState.Failure;
    }

    // 추적 실행
    private NodeState TrackTarget()
    {
        _agent.isStopped = false;
        _agent.SetDestination(_target.position);

        _anim.SetBool("IsMoving", true);
        return NodeState.Success;
    }

    // 대기
    private NodeState Idle()
    {
        _agent.isStopped = true;
        _anim.SetBool("IsMoving", false);
        return NodeState.Success;
    }

    // 피격 처리 (Bullet에서 호출)
    public void OnDamaged()
    {
        // 피격 애니메이션 재생 등
        // _anim.SetTrigger("Hit");
    }


    // 간단한 Action 노드 래퍼
    public class ActionNode : Node
    {
        // 반환값이 NodeState인 함수(델리게이트)를 저장
        public delegate NodeState ActionDelegate();
        private ActionDelegate _action;

        public ActionNode(ActionDelegate action) { _action = action; }

        public override NodeState Evaluate()
        {
            return _action();
        }
    }
}
