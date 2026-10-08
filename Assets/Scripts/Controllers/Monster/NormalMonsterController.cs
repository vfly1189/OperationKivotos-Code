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

    // 리쉬(귀환) — ① 집에서 이 거리보다 멀어지거나 ② 쫓던 대상이 감지 거리 밖으로 사라지면 집으로 돌아간다.
    //  귀환이 끝날 때까지 감지·추적·공격을 하지 않고 무적이며, 도착하면 체력·탄약 원복.
    //  어떤 활성 정책(Sector · 거리 · 청크)이든 똑같이 적용되는 몬스터 쪽 규칙이다. docs/FieldEntity/Sector_Problems.md 4절.
    [Header("Leash")]
    [SerializeField] protected float _leashRange = 25f;
    [SerializeField] protected float _returnTimeout = 8f;     // 최소 귀환 시간 한도. 실제 한도는 거리/속도의 2배와 비교해 큰 쪽 — 넘으면 막힌 것으로 보고 순간이동

    // Before 녹화(리쉬 없는 원래 Sector) 용 전역 스위치. BenchmarkTester F3.
    public static bool LeashEnabled = true;

    // 귀환 시작 · 완료 로그. 측정 중엔 끈다 — Debug.Log가 몬스터 Update 비용에 섞인다. 시간 초과 경고는 이상 상황이라 항상 남긴다.
    public static bool LogLeash = false;

    protected NavMeshAgent _agent;
    protected UI_MonsterHPBar _hpBar;

    private Vector3 _lastDestPosition = Vector3.zero;

    private Vector3 _homePosition;
    private bool _leashActive;          // 필드 스포너가 만든 몹만 켠다 — 던전 웨이브 · 보스 소환 몹은 리쉬 없음
    private bool _isReturning;          // _state와 따로 둔다 — 공격/장전 애니 이벤트가 _state를 덮어쓰기 때문
    private float _returnDeadline;
    private float _chaseStoppingDistance;

    private const float AwayFromHome = 2f;   // 이보다 집에서 떨어져 있고 대상이 없으면 "쫓아왔다가 놓친 것" (도착 판정 1m보다 커야 왕복 안 함)

    protected override void Awake()
    {
        base.Awake();

        //NavMeshAgent 캐싱 & 파라미터 세팅
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();

        _agent.speed = _moveSpeed;
        _chaseStoppingDistance = _attackRange - 0.5f;
        _agent.stoppingDistance = _chaseStoppingDistance;
        _agent.updateRotation = true;
    }

    protected override void Start()
    {
        base.Start();

        //await CreateHpBarAsync();
        //_initialized = true;

        //if (Managers.Party.GetCurrentCharacter() != null)
        //    UpdateTarget(Managers.Party.GetCurrentCharacter().gameObject);

        //if (Managers.Party != null)
        //{
        //    Managers.Party.OnActiveCharacterChanged -= OnPlayerCharacterChanged;
        //    Managers.Party.OnActiveCharacterChanged += OnPlayerCharacterChanged;
        //}
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // 배치는 꺼진 상태에서 끝났으므로(ResourceManager) 대기 없이 바로 켠다 — 구 1프레임 대기 + Warp 제거.
        //  생애 밖의 OnEnable(풀 프리웜의 Create)에서는 켜지 않는다: 원점이 NavMesh 밖이면 agent 생성 실패 경고.
        if (IsSpawned && _agent != null)
            _agent.enabled = true;
    }
    
    public override void OnSpawn(SpawnContext ctx)
    {
        base.OnSpawn(ctx);

        // 이전 생애의 이동·리쉬 상태를 지운다. 복귀 중 회수됐다 재사용되면 무적·정지거리가 남아 있을 수 있다.
        // 집 = 스폰 위치를 NavMesh 위로 맞춘 점. 포인트가 메시에서 살짝 떠 있으면 "집에서 떨어짐"으로 오판해 귀환을 반복한다.
        _homePosition = NavMesh.SamplePosition(ctx.Position, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : ctx.Position;
        _lastDestPosition = Vector3.zero;
        _leashActive = false;
        _isReturning = false;
        _agent.stoppingDistance = _chaseStoppingDistance;
        Stat.HealthComp.IsInvincible = false;

        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= OnPlayerCharacterChanged;
            Managers.Party.OnActiveCharacterChanged += OnPlayerCharacterChanged;

            if (Managers.Party.GetCurrentCharacter() != null)
                UpdateTarget(Managers.Party.GetCurrentCharacter().gameObject);
        }

        BindHpBar();
    }

    public override void OnDespawn()
    {
        ReleaseHpBar();
        Managers.Party.OnActiveCharacterChanged -= OnPlayerCharacterChanged;      
        base.OnDespawn();
    }

    void BindHpBar()
    {
        GameObject prefab = Managers.Resource.GetLoaded<GameObject>("MonsterHPBar");
        _hpBar = Managers.Resource.Instantiate(prefab, Managers.UI.CanvasWorld.transform)
                                  .GetComponent<UI_MonsterHPBar>();
        _hpBar.SetTarget(_hpBarTransform, Stat);
        Stat.HealthComp.OnHpChanged += _hpBar.UpdateHpBar;
    }

    void ReleaseHpBar()            // 사망 경로와 OnDespawn이 같이 부름 → 두 번 불려도 안전
    {
        if (_hpBar == null) return;
        Stat.HealthComp.OnHpChanged -= _hpBar.UpdateHpBar;
        Managers.Resource.Destroy(_hpBar.gameObject);
        _hpBar = null;
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

        // 0.5 리쉬 노드 — 사망 다음, 전투보다 앞. 복귀 중엔 공격·추적을 하지 않는다.
        Node leashNode = new ActionNode(HandleLeash);

        _topNode = new Selector(new List<Node>
        {
            deadSequence,
            leashNode,
            combatNode,
            chaseSequence,
            new ActionNode(Idle)
        });
    }

    #region Leash

    // 귀환 중이 아니고 귀환할 이유도 없으면 Failure → 다음 노드(전투·추적)로.
    //  귀환 중이면 Running — Selector가 여기서 멈추므로 뒤의 감지·추적·공격 노드는 평가조차 되지 않는다.
    private NodeState HandleLeash()
    {
        if (!_isReturning)
        {
            if (!LeashEnabled || !_leashActive) return NodeState.Failure;

            float homeSqr = HorizontalSqrDistance(transform.position, _homePosition);
            if (homeSqr > _leashRange * _leashRange) StartReturn("너무 멀어짐");
            else if (homeSqr > AwayFromHome * AwayFromHome && !IsTargetDetected()) StartReturn("대상 놓침");
            else return NodeState.Failure;
        }

        if (_agent == null || !_agent.enabled) return NodeState.Running;

        // hasPath 확인: 경로 계산 실패 시 remainingDistance가 0으로 읽혀 "도착"으로 오판 → 매 프레임 복귀 재시작을 막는다.
        bool arrived = HorizontalSqrDistance(transform.position, _homePosition) <= 1f
                       || (!_agent.pathPending && _agent.hasPath && _agent.remainingDistance <= 0.3f);

        if (!arrived && Time.time > _returnDeadline)
        {
            GameLog.LogWarning($"[Leash] {name} 복귀 시간 초과 — 집으로 순간이동");
            _agent.Warp(_homePosition);
            arrived = true;
        }

        if (!arrived) return NodeState.Running;

        FinishReturn();
        return NodeState.Success;
    }

    private void StartReturn(string reason)
    {
        _isReturning = true;
        // 고정 8초로는 25m 귀환(AR 3.5m/s ≈ 7초 + 우회)이 막히지 않았는데도 시간 초과가 났다 → 거리 기준으로.
        float distance = Mathf.Sqrt(HorizontalSqrDistance(transform.position, _homePosition));
        float speed = (_agent != null && _agent.speed > 0.1f) ? _agent.speed : _moveSpeed;
        _returnDeadline = Time.time + Mathf.Max(_returnTimeout, distance / speed * 2f);
        Stat.HealthComp.IsInvincible = true;          // 복귀 중 무적 (끌고 가서 때리기 방지)

        if (LogLeash) GameLog.Log($"[Leash] {name} 귀환 시작 ({reason}) — 집에서 {distance:F1}m");

        // 공격·장전 중이었어도 끊는다. 애니 이벤트가 _state를 바꿔도 _isReturning이 우선.
        _state = MonsterState.Moving;
        _anim.CrossFade("Move", 0.1f);

        if (_agent != null && _agent.enabled)
        {
            _agent.stoppingDistance = 0f;             // 추적용 정지거리(사거리-0.5)로는 집 앞 수 m에서 멈춘다
            _agent.isStopped = false;
            _agent.SetDestination(_homePosition);
        }
    }

    private void FinishReturn()
    {
        if (LogLeash) GameLog.Log($"[Leash] {name} 귀환 완료 — 체력 원복");
        _isReturning = false;
        Stat.HealthComp.IsInvincible = false;
        Stat.HealthComp.Recover();                    // 체력 원복 — HP바는 OnHpChanged로 갱신

        _lastDestPosition = Vector3.zero;             // 다음 추적 때 경로를 새로 잡게
        if (Managers.Party?.GetCurrentCharacter() != null)
            UpdateTarget(Managers.Party.GetCurrentCharacter().gameObject);   // 귀환 중 캐릭터가 바뀌었을 수 있다
        if (_agent != null && _agent.enabled)
        {
            _agent.stoppingDistance = _chaseStoppingDistance;
            _agent.isStopped = true;
        }

        _state = MonsterState.Idle;
        _anim.CrossFade("Idle", 0.1f);

        OnReturnedHome();
    }

    // 교전 = 살아 있고 (귀환 · 추적 · 공격 · 장전 중이거나 대상이 감지 거리 안).
    //  감지 거리를 같이 보는 이유: 공격·장전 종료 애니 이벤트가 _state를 Idle로 돌린 뒤 다음 행동 트리 평가 전까지
    //  한 프레임 틈이 있다. 스포너 틱이 그 틈에 읽으면 싸우는 중인 몹을 회수한다.
    //  끝나는 것은 리쉬가 보장한다 — 대상을 놓치면 귀환하고, 집에 도착하면 Idle.
    public override bool IsEngaged
    {
        get
        {
            if (_state == MonsterState.Dead || Stat.HealthComp.CurrentHp <= 0) return false;
            return _isReturning
                || _state == MonsterState.Moving || _state == MonsterState.Attacking || _state == MonsterState.Reloading
                || IsTargetDetected();
        }
    }

    // 필드 규칙이라 SpawnerManager가 스폰 직후 켠다. 끄는 건 다음 생애의 OnSpawn.
    public void EnableLeash() => _leashActive = true;

    // 집에 도착한 순간 — 하위 클래스가 생애 중 상태(탄약 등)를 원복한다.
    protected virtual void OnReturnedHome() { }

    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    #endregion


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


        ReleaseHpBar();

        CallOnDead();

        return base.HandleDeadState();
    }

   

    private NodeState CheckDetectRange()
    {
        return IsTargetDetected() ? NodeState.Success : NodeState.Failure;
    }

    private bool IsTargetDetected()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy) return false;
        return Vector3.Distance(transform.position, _target.position) <= _detectRange;
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
        if (_isReturning) return;   // 공격 애니에서 Move로 블렌드되는 중에도 이벤트가 올 수 있다
        if (sfx != null) Managers.Sound.Play(sfx, Define.Sound.Effect);
        PerformAttackAction();
    }
    protected abstract void PerformAttackAction(); // 실제 투사체 발사나 타격 판정
}
