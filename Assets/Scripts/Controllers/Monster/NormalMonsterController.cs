using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;

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
    private bool _initialized; // 최초 Start 완료 여부 (풀 재사용 시 HP바 재생성 판단용)

    
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

        await CreateHpBarAsync();
        _initialized = true;

        if (Managers.Party.GetCurrentCharacter() != null)
            UpdateTarget(Managers.Party.GetCurrentCharacter().gameObject);

        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= OnPlayerCharacterChanged;
            Managers.Party.OnActiveCharacterChanged += OnPlayerCharacterChanged;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // 풀 재사용 시: 사망 때 파괴된 HP바를 다시 생성 (최초 Start 이후에만)
        if (_initialized && _hpBar == null)
            CreateHpBarAsync().Forget();
    }



    // HP바 생성 + 스탯 이벤트 구독 (최초 스폰/풀 재사용 공용)
    private async UniTask CreateHpBarAsync()
    {
        // 이번 활성 주기의 수명 토큰 캡처 (OnDisable/사망 시 취소됨)
        CancellationToken token = _monsterCts != null ? _monsterCts.Token : CancellationToken.None;

        Transform uiParent = Managers.UI.CanvasWorld.transform;
        UI_MonsterHPBar hpBar;
        try
        {
            hpBar = await Managers.UI.MakeSubItemAsync<UI_MonsterHPBar>("MonsterHPBar", uiParent, token);
        }
        catch (OperationCanceledException)
        {
            return; // 로드 대기 중 디스폰/사망으로 취소됨
        }
        if (hpBar == null) return;

        // 생성 대기 중 디스폰/사망/풀 반환이 일어났으면 방금 만든 바를 즉시 회수 (유령 HP바 누수 방지)
        if (this == null || !isActiveAndEnabled || token.IsCancellationRequested)
        {
            Managers.Resource.Destroy(hpBar.gameObject);
            return;
        }

        _hpBar = hpBar;
        _hpBar.SetTarget(_hpBarTransform, Stat);
        Stat.OnHpChanged -= _hpBar.UpdateHpBar;
        Stat.OnHpChanged += _hpBar.UpdateHpBar;
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


        if (_hpBar != null)
        {
            //_hpBar.gameObject.SetActive(false);
            
            Stat.OnHpChanged -= _hpBar.UpdateHpBar;      // 구독 해제
            Managers.Resource.Destroy(_hpBar.gameObject); // 실제 파괴(또는 풀 반환)
            _hpBar = null;
            
        }

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
