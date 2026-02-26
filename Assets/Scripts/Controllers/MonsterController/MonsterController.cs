using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class MonsterController : MonoBehaviour
{
    // [추가] 몬스터의 현재 행동 상태를 명확히 정의
    public enum MonsterState
    {
        Spawning,   // 스폰 및 준비 중 (Agent 활성화 전)
        Idle,       // 대기
        Moving,     // 이동
        Attacking,  // 공격 중
        Reloading,  // 장전 중
        Dead        // 사망
    }

    [Header("Settings")]
    [SerializeField] private float _detectRange = 10f; // 감지 범위
    [SerializeField] private float _attackRange = 2f;  // 공격 범위
    [SerializeField] private float _moveSpeed = 3.5f;
    [SerializeField] private int _maxAmmo = 3; // 공격 3번 후 재장전

    [Header("UI Settings")]
    [SerializeField] private GameObject _hpBarPrefab; // [핵심] 인스펙터에서 프리팹 직접 할당!
    [SerializeField] private Transform _hpBarTransform;

    [Header("Components")]
    [SerializeField] private Animator _anim;
    [SerializeField] private NavMeshAgent _agent; // 길찾기용 (필수 아님, Transform이동이면 제거)

    // 스탯 (필수 재사용)
    public MonsterStat Stat { get; private set; }

    // 타겟(플레이어)
    protected Transform _target;
    // 행동 트리 루트 노드
    private Node _topNode;

    // [상태 관리 변수]
    // [핵심] 여러 bool 변수들을 하나의 State로 통합
    protected MonsterState _state = MonsterState.Spawning;
    private int _currentAmmo;

    private UI_MonsterHPBar _hpBar;

    private void Awake()
    {
        Stat = GetComponent<MonsterStat>();
        Stat?.Init();

        if (_anim == null) _anim = GetComponent<Animator>();
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();

        if (_agent != null)
        {
            _agent.speed = _moveSpeed;
            _agent.stoppingDistance = _attackRange - 0.5f;
            _agent.updateRotation = true;
            _agent.enabled = false;
        }

        _currentAmmo = _maxAmmo; // 탄창 초기화
        ConstructBehaviorTree();
    }

    private void Start()
    {    
        if (_hpBarPrefab != null)
        {
            GameObject canvasObj = GameObject.Find("@GameSceneCanvas");
            Transform uiParent = canvasObj != null ? canvasObj.transform : null;

            _hpBar = Managers.UI.MakeSubItem<UI_MonsterHPBar>(_hpBarPrefab, uiParent);
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


    private void OnDestroy()
    {
        if (Managers.Party != null)
            Managers.Party.OnActiveCharacterChanged -= OnPlayerCharacterChanged;
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

    // 풀링 사용 시 OnEnable에서 초기화 필요
    private void OnEnable()
    {
        _state = MonsterState.Spawning; // 시작 상태 초기화

        _currentAmmo = _maxAmmo;

        // 스탯 초기화 (죽은 상태 복구)
        Stat?.Init();

        // 컴포넌트 재활성화
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true; // 죽을 때 껐던 콜라이더 다시 켜기

        // 애니메이션 리셋 (중요: 죽는 모션에서 바로 Idle로)
        if (_anim != null)
        {
            _anim.Rebind(); // 애니메이터 완전 초기화
            _anim.Play("Appear");
        }

        if(_hpBar != null)
        {
            _hpBar.gameObject.SetActive(true);
            _hpBar.UpdateHpBar(Stat.CurrentHp, Stat.MaxHp.Value);
        }

        StartCoroutine(CoEnableAgentDelay());
    }

    private IEnumerator CoEnableAgentDelay()
    {
        yield return null;

        if (_state != MonsterState.Dead && _agent != null)
        {
            _agent.enabled = true;
        }

        // 준비가 끝났으므로 Idle 상태로 전환하여 AI 작동 시작
        _state = MonsterState.Idle;
    }


    // 풀로 돌아갈 때(OnDisable) 꺼주는 로직 필수
    protected void OnDisable()
    {
        if (_agent != null && _agent.enabled)
        {
            _agent.enabled = false;
        }
        StopAllCoroutines();
    }


    private void Update()
    {
        // Spawning(준비중)이거나 Dead 상태면 행동 트리 실행 안함
        if (_state == MonsterState.Spawning || _state == MonsterState.Dead)
            return;

        _topNode?.Evaluate();
    }

    private void ConstructBehaviorTree()
    {
        // 0. 사망 노드 (Sequence: 죽었는가? -> 사망 연출 실행)
        Node deadSequence = new Sequence(new List<Node>
        {
            new ActionNode(CheckIsDead),
            new ActionNode(HandleDeadState)
        });

        // 1. 공격 노드 구성
        //    (사거리 내인가? -> 공격 or 재장전 실행)
        Node attackSequence = new Sequence(new List<Node>
        {
            new ActionNode(CheckAttackRange),
            new ActionNode(HandleCombat) // 공격+재장전 통합 처리
        });

        // 2. 추적 노드
        Node chaseSequence = new Sequence(new List<Node>
        {
            new ActionNode(CheckDetectRange),
            new ActionNode(TrackTarget)
        });

        // 3. 최상위 Selector
        _topNode = new Selector(new List<Node>
        {
            deadSequence,
            attackSequence, // 공격 중이면 무조건 Running 유지됨
            chaseSequence,
            new ActionNode(Idle)
        });
    }

    // ========================================================================
    // [Action Nodes]
    // ========================================================================

    private NodeState CheckAttackRange()
    {
        // 공격 중이거나 장전 중이면 거리가 멀어져도 동작을 마칠 수 있도록 유지
        if (_state == MonsterState.Attacking || _state == MonsterState.Reloading)
            return NodeState.Success;

        if (_target == null) return NodeState.Failure;
        float distance = Vector3.Distance(transform.position, _target.position);

        return (distance <= _attackRange) ? NodeState.Success : NodeState.Failure;
    }

    // 전투 처리 (공격 vs 재장전 분기)
    private NodeState HandleCombat()
    {
        // 1. 이미 행동 중이면 대기
        if (_state == MonsterState.Reloading || _state == MonsterState.Attacking)
        {
            if (_state == MonsterState.Attacking) RotateToTarget();
            return NodeState.Running;
        }

        // 2. 탄알이 없다면 장전
        if (_currentAmmo <= 0)
        {
            StartReload();
            return NodeState.Running;
        }

        // 3. 공격 시작
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

    // 조건: HP가 0 이하인가? 
    private NodeState CheckIsDead()
    {
        return (Stat.CurrentHp <= 0) ? NodeState.Success : NodeState.Failure;
    }

    // 행동: 사망 처리 및 대기
    private NodeState HandleDeadState()
    {
        if (_state == MonsterState.Dead) return NodeState.Running;

        _state = MonsterState.Dead; // 사망 상태로 고정

        if (_agent != null)
        {
            _agent.isStopped = true;
            _agent.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        _anim.CrossFade("Death", 0.1f);

        if (_hpBar != null) _hpBar.gameObject.SetActive(false);

        StartCoroutine(CoDespawn());
        return NodeState.Running;
    }

    // ------------------------------------------------------------------------
    // [애니메이션 이벤트] (Animation Clip에 반드시 이벤트를 심어야 함!)
    // ------------------------------------------------------------------------

    public void OnAttackFinished()
    {
        if (_state != MonsterState.Dead) _state = MonsterState.Idle;
    }

    public void OnReloadFinished()
    {
        _currentAmmo = _maxAmmo; // 탄알 충전
        if (_state != MonsterState.Dead) _state = MonsterState.Idle;
    }

    public void OnAttackEvent(AudioClip sfx)
    {
        Managers.Sound.Play(sfx, Define.Sound.Effect);
        PerformAttackAction();
    }

    protected virtual void PerformAttackAction() { }

    // ------------------------------------------------------------------------

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
            _agent.SetDestination(_target.position);
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

    private void RotateToTarget()
    {
        if (_target == null) return;
        Vector3 dir = (_target.position - transform.position).normalized;
        if (dir != Vector3.zero)
        {
            dir.y = 0; // Y축 회전 방지
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }
    }

   
    private IEnumerator CoDespawn()
    {
        yield return new WaitForSeconds(2.0f);
        Managers.Resource.Destroy(gameObject);
    }
}
