using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using static MonsterController;

// 보스를 제외하고 NavMeshAgent로 맵을 돌아다니며 플레이어를 추적하는 몬스터.

public abstract class NormalMonsterController : BaseMonsterController
{
    [Header("Base Settings")]
    [SerializeField] protected float _detectRange = 10f;
    [SerializeField] protected float _attackRange = 2f;
    [SerializeField] protected float _moveSpeed = 3.5f;

    [SerializeField] private Transform _hpBarTransform;

    protected NavMeshAgent _agent;
    protected Transform _target;
    protected UI_MonsterHPBar _hpBar;

    
    private Vector3 _lastDestPosition = Vector3.zero;

    protected override void Awake()
    {
        base.Awake();

        if (_agent == null) _agent = GetComponent<NavMeshAgent>();

        if (_agent != null)
        {
            _agent.speed = _moveSpeed;
            _agent.stoppingDistance = _attackRange - 0.5f;
            _agent.updateRotation = true;
            _agent.enabled = false;
        }
    }

    protected override async void Start()
    {
        base.Start();

        Transform uiParent = Managers.UI.CanvasWorld.transform;

        _hpBar = await Managers.UI.MakeSubItemAsync<UI_MonsterHPBar>("MonsterHPBar", uiParent);

        if (_hpBar != null)
        {
            _hpBar.SetTarget(_hpBarTransform, Stat);

            Stat.OnHpChanged -= _hpBar.UpdateHpBar;
            Stat.OnHpChanged += _hpBar.UpdateHpBar;
        }
        
        if (Managers.Party.GetCurrentCharacter() != null)
            UpdateTarget(Managers.Party.GetCurrentCharacter().gameObject);

        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= OnPlayerCharacterChanged;
            Managers.Party.OnActiveCharacterChanged += OnPlayerCharacterChanged;
        }
    }

    // 타겟 갱신 함수
    private void UpdateTarget(GameObject player)
    {
        if (player != null) _target = player.transform;
    }

    // 캐릭터 교체 이벤트 핸들러
    private void OnPlayerCharacterChanged(GameObject player)
    {
        UpdateTarget(player);
    }

    protected override void ConstructBehaviorTree()
    {
        // 0. 사망 노드
        Node deadSequence = new Sequence(new List<Node> 
        { 
            new ActionNode(CheckIsDead), 
            new ActionNode(HandleDeadState) 
        });

        // 1. 전투 노드 (자식 클래스마다 공격/장전 방식이 다르므로 추상화하여 받아옴)
        Node combatNode = GetCombatNode();

        // 2. 추적 노드
        Node chaseSequence = new Sequence(new List<Node> 
        { 
            new ActionNode(CheckDetectRange), 
            new ActionNode(TrackTarget) 
        });

        _topNode = new Selector(new List<Node> 
        { 
            deadSequence, 
            combatNode, 
            chaseSequence, 
            new ActionNode(Idle) 
        });
    }


    protected abstract Node GetCombatNode();


    protected override NodeState HandleDeadState()
    {
        // 중복 실행 방지 
        if (_state == MonsterState.Dead) return NodeState.Running;

      

        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.enabled = false;
        }


        if (_hpBar != null) _hpBar.gameObject.SetActive(false);

        CallOnDead();

        return base.HandleDeadState();
    }

   

    private NodeState CheckDetectRange()
    {
        if (_target == null) return NodeState.Failure;
        float distance = Vector3.Distance(transform.position, _target.position);
        return (distance <= _detectRange) ? NodeState.Success : NodeState.Failure;
    }

    private NodeState TrackTarget()
    {
        // 공격이나 장전 중이면 추적 불가
        if (_state == MonsterState.Attacking || _state == MonsterState.Reloading)
            return NodeState.Failure;

        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = false;

            // [최적화 핵심] 타겟의 위치가 이전 위치와 0.5f 이상 차이 날 때만 길찾기 연산 수행!
            if (Vector3.SqrMagnitude(_target.position - _lastDestPosition) > 0.25f)
            {
                _lastDestPosition = _target.position;
                _agent.SetDestination(_target.position);
            }
        }

        if (_state != MonsterState.Moving)
        {
            _state = MonsterState.Moving;
            _anim.CrossFade("Move", 0.1f);
        }

        return NodeState.Success;
    }

    private NodeState Idle()
    {
        if (_state == MonsterState.Attacking || _state == MonsterState.Reloading)
            return NodeState.Running;

        if (_agent != null && _agent.enabled) _agent.isStopped = true;

        if (_state != MonsterState.Idle)
        {
            _state = MonsterState.Idle;
            _anim.CrossFade("Idle", 0.1f);
        }

        return NodeState.Success;
    }

    protected void RotateToTarget()
    {
        if (_target == null) return;
        Vector3 dir = (_target.position - transform.position).normalized;
        if (dir != Vector3.zero)
        {
            dir.y = 0; // Y축 회전 방지
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }
    }

    // 애니메이션 이벤트 (공통)
    public void OnAttackEvent(AudioClip sfx)
    {
        if (sfx != null) Managers.Sound.Play(sfx, Define.Sound.Effect);
        PerformAttackAction();
    }
    protected abstract void PerformAttackAction(); // 실제 투사체 발사나 타격 판정
}
