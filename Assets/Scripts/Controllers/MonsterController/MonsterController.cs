using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class MonsterController : MonoBehaviour
{
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
    private Transform _target;
    // 행동 트리 루트 노드
    private Node _topNode;

    // [상태 관리 변수]
    private int _currentAmmo;
    private bool _isAttacking = false; // 공격 사이클 진행 중인가?
    private bool _isReloading = false; // 재장전 중인가?
    private bool _isMoving = false;
    private bool _isDeadProcessed = false; // 사망 로직이 이미 실행되었는지 확인용

    private UI_MonsterHPBar _hpBar;
    private GameObject _followTarget;
    private void Awake()
    {
        Stat = GetComponent<MonsterStat>();

        if (Stat != null)
        {
            Stat.Init();
        }

        if (_anim == null) _anim = GetComponent<Animator>();
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();

        if (_agent != null)
        {
            _agent.speed = _moveSpeed;
            _agent.stoppingDistance = _attackRange - 0.5f;
            _agent.updateRotation = true;
        }

        _currentAmmo = _maxAmmo; // 탄창 초기화
        ConstructBehaviorTree();
    }

    private void Start()
    {
        // [핵심] UIManager에게 "내가 들고 있는 이 프리팹으로 만들어줘" 요청
        if (_hpBarPrefab != null)
        {
            // 부모를 null로 주면 UIManager가 알아서 SceneUI나 Canvas 밑으로 넣어줌
            _hpBar = Managers.UI.MakeSubItem<UI_MonsterHPBar>(_hpBarPrefab, null);

            // 타겟 세팅
            _hpBar.SetTarget(_hpBarTransform, Stat);

            Stat.OnHpChanged -= _hpBar.UpdateHpBar;
            Stat.OnHpChanged += _hpBar.UpdateHpBar;
        }

        // [수정] PartyManager를 통해 현재 플레이어 타겟 가져오기
        UpdateTarget();

        // [핵심] 캐릭터 교체 이벤트 구독
        if (PartyManager.Instance != null)
        {
            PartyManager.Instance.OnCharacterChanged -= OnPlayerCharacterChanged;
            PartyManager.Instance.OnCharacterChanged += OnPlayerCharacterChanged;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (PartyManager.Instance != null)
        {
            PartyManager.Instance.OnCharacterChanged -= OnPlayerCharacterChanged;
        }
    }

    // [추가] 타겟 갱신 함수
    private void UpdateTarget()
    {
        _target = PartyManager.Instance.GetCurrentCharacter().gameObject.transform;

        if (_target == null)
        {
            // 만약 PartyManager가 없다면 예전 방식으로 폴백
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
        }
    }

    // [추가] 캐릭터 교체 이벤트 핸들러
    private void OnPlayerCharacterChanged(int newIndex)
    {
        Debug.Log($"Monster: Player switched to character {newIndex}, updating target.");
        UpdateTarget();
    }

    // 풀링 사용 시 OnEnable에서 초기화 필요
    private void OnEnable()
    {
        _isDeadProcessed = false;
        _isAttacking = false;
        _isReloading = false;
        _currentAmmo = _maxAmmo;

        if (_agent != null) _agent.enabled = true;
        if (_anim != null) _anim.Play("Idle"); // 초기 상태

        // HP바 생성 등은 Start 혹은 여기서 처리
    }


    private void Update()
    {
        if (_topNode != null)
            _topNode.Evaluate();
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
        // [중요] 이미 공격이나 재장전 중이라면, 사거리 벗어나도 행동을 마쳐야 함 -> Success 반환
        if (_isAttacking || _isReloading) return NodeState.Success;

        if (_target == null) return NodeState.Failure;
        float distance = Vector3.Distance(transform.position, _target.position);

        return (distance <= _attackRange) ? NodeState.Success : NodeState.Failure;
    }

    // 전투 처리 (공격 vs 재장전 분기)
    private NodeState HandleCombat()
    {
        // 1. 재장전 중이라면? -> 재장전 끝날 때까지 Running
        if (_isReloading)
        {
            return NodeState.Running;
        }

        // 2. 공격 중이라면? -> 공격 끝날 때까지 Running
        if (_isAttacking)
        {
            // (선택) 공격 중에도 타겟 회전
            RotateToTarget();
            return NodeState.Running;
        }

        // 3. 탄알이 없다면? -> 재장전 시작
        if (_currentAmmo <= 0)
        {
            StartReload();
            return NodeState.Running;
        }

        // 4. 모든 조건 만족 -> 새 공격 시작
        StartAttack();
        return NodeState.Running; // 공격 시작했으니 Running 반환
    }
    
    private void StartAttack()
    {
        _isAttacking = true;
        _agent.isStopped = true;
        _isMoving = false;

        // 애니메이션 시작 (Start -> Ing -> Delay -> End 순서로 흘러감)
        _anim.CrossFade("Attack_Start", 0.1f);

        // 탄알 차감
        _currentAmmo--;
        Debug.Log($"공격 시작! 남은 탄: {_currentAmmo}");
    }

    private void StartReload()
    {
        _isReloading = true;
        _agent.isStopped = true;
        _isMoving = false;

        _anim.CrossFade("Reload", 0.1f);
        Debug.Log("재장전 시작...");
    }

    // 조건: HP가 0 이하인가? (Stat.IsDead 활용)
    private NodeState CheckIsDead()
    {
        // Stat에 IsDead 프로퍼티가 있다고 가정 (CurrentHp <= 0)
        if (Stat.CurrentHp <= 0)
        {
            return NodeState.Success; // 죽었음 -> 다음 노드(HandleDeadState)로 진행
        }

        return NodeState.Failure; // 살았음 -> 다음 가지(공격)로 넘어감
    }

    // 행동: 사망 처리 및 대기
    private NodeState HandleDeadState()
    {
        // 1. 이미 사망 처리가 시작되었다면? -> 계속 Running 유지 (사라질 때까지)
        if (_isDeadProcessed)
        {
            return NodeState.Running;
        }

        // 2. 사망 처리 최초 진입
        _isDeadProcessed = true;
        Debug.Log("BehaviorTree: Monster Dead Logic Start");

        // 이동 정지 및 콜라이더 해제
        if (_agent != null)
        {
            _agent.isStopped = true;
            _agent.enabled = false;
        }
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 애니메이션
        _anim.CrossFade("Death", 0.1f);

        // HP바 삭제
        if (_hpBar != null)
        {
            Managers.Resource.Destroy(_hpBar.gameObject);
            _hpBar = null;
        }

        // 2초 뒤 삭제 코루틴 시작
        StartCoroutine(CoDespawn());

        return NodeState.Running; // 삭제될 때까지 이 상태를 유지
    }

    // ------------------------------------------------------------------------
    // [애니메이션 이벤트] (Animation Clip에 반드시 이벤트를 심어야 함!)
    // ------------------------------------------------------------------------

    // Attack_End 애니메이션의 끝부분에 심으세요.
    public void OnAttackFinished()
    {
        Debug.Log("공격 사이클 종료");
        _isAttacking = false; // 상태 해제 -> 다음 프레임에 트리에서 다시 판단
    }

    // Reload 애니메이션의 끝부분에 심으세요.
    public void OnReloadFinished()
    {
        Debug.Log("재장전 완료");
        _currentAmmo = _maxAmmo; // 탄알 충전
        _isReloading = false;
    }

    // Attack_Ing 등의 타이밍에 실제 데미지 판정용
    public void OnAttackHit()
    {
        // 플레이어에게 데미지 주기
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
        if (_isAttacking || _isReloading) return NodeState.Failure; // 공격 중 이동 불가

        _agent.isStopped = false;
        _agent.SetDestination(_target.position);

        if (!_isMoving)
        {
            _isMoving = true; // 이동 상태로 변경
            _anim.CrossFade("Move", 0.1f);
            Debug.Log("무빙 애니메이션 재생 (최초 1회)");
        }

        return NodeState.Success;
    }

    private NodeState Idle()
    {
        if (_isAttacking || _isReloading) return NodeState.Running;

        _agent.isStopped = true;

        _anim.CrossFade("Idle", 0.1f);

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

    // Node 클래스들 (그대로 유지)
    public class ActionNode : Node
    {
        public delegate NodeState ActionDelegate();
        private ActionDelegate _action;
        public ActionNode(ActionDelegate action) { _action = action; }
        public override NodeState Evaluate() { return _action(); }
    }

    private IEnumerator CoDespawn()
    {
        yield return new WaitForSeconds(2.0f);
        Managers.Resource.Destroy(gameObject);
    }
}
