using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


// 보스 몬스터, 일반 몬스터 모두 공통으로 쓰는 기능들이 있어야됨.
// 공통 인것들
// Awake OnEnable OnDisable Update
// CheckIsDead


// 보스몹 - 입장연출 , 사망연출이 존재
// 일반몹 -  

public abstract class BaseMonsterController : MonoBehaviour, IAbilityCaster, IMonsterLifecycle
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
    protected MonsterState _state = MonsterState.Spawning;

    #region Components
    protected Animator _anim;
    private MonsterStat _stat;
    protected Collider _col;

    // 외부에서 Stat을 요구할 때
    public MonsterStat Stat
    {
        get
        {
            if (_stat == null)
            {
                _stat = GetComponent<MonsterStat>();
                if (_stat != null) _stat.Init();
            }
            return _stat;
        }
    }
    #endregion


    protected Node _topNode;
    protected CancellationTokenSource _monsterCts;
    // 생애 중(OnSpawn ~ OnDespawn)인가. CTS는 OnSpawn에서만 생기고 OnDespawn/OnDisable에서 사라진다.
    //  풀 프리웜의 Create처럼 생애 밖에서 도는 OnEnable을 구분하는 데 쓴다.
    protected bool IsSpawned => _monsterCts != null;
    protected Transform _target;

    // 교전 중인가 — 스포너가 꺼져도 회수를 미룰지 판단한다(SpawnerManager 교전 유지). 리쉬가 없는 몹은 교전이 안 끝날 수 있으니 기본 false.
    public virtual bool IsEngaged => false;

    public event Action OnDead;         // 체력이 0이 되는 순간
    public event Action<BaseMonsterController> OnDespawned;    // 시체 연출까지 끝나고 오브젝트가 사라지기 직전 (스포너 리스폰용)

    [SerializeField] public List<AbilityData> _abilities = new List<AbilityData>();
    protected AbilityRunner _abilityRunner = new AbilityRunner();


    // Awake - OnEnable - Start - FixedUpdate - OnTrigger - OnCollision - Update - LateUpdate - OnDisable - Destroy
    protected virtual void Awake()
    {       
        if (_anim == null) _anim = GetComponent<Animator>();
        if(_col == null ) _col = GetComponent<Collider>();


        var tempStat = Stat;
        // 보상 지급 바인딩
        MonsterReward.EnsureOn(gameObject, tempStat);
        // 데미지 표시 바인딩
        DamageNumberPresenter.EnsureOn(gameObject, tempStat);


        ConstructBehaviorTree(); // 자식에서 구현
    }

    protected virtual void OnEnable() 
    {
        //CancelMonsterTasks();
        //_monsterCts = new CancellationTokenSource();
        //_state = MonsterState.Spawning;

        //// 풀 재사용 시 이전 생애의 리스폰 구독(람다)이 남아 중복 리스폰되는 것을 방지
        //OnDespawned = null;

        //Stat?.HealthComp.Recover();

        if (_col != null) _col.enabled = true;
        if (_anim != null)
        {
            _anim.Rebind();
            _anim.Play("Appear");
        }
    }
    protected virtual void Start() { }

    protected virtual void Update()
    {
        // 죽었거나 행동 트리가 없다면 실행하지 않음
        if (_state == MonsterState.Dead || _topNode == null)
            return;

        // 매 프레임 행동 트리 평가(실행)
        _topNode.Evaluate();
    }

    protected virtual void OnDisable()
    {
        CancelMonsterTasks();
    }
    protected virtual void OnDestroy() { }


    public virtual void OnSpawn(SpawnContext ctx)
    {  
        _monsterCts = new CancellationTokenSource();
        _state = MonsterState.Spawning;

        OnDead = null;
        OnDespawned = null;

        Stat?.HealthComp.Recover();
        _target = null;

        _topNode.Reset();
    }

    public virtual void OnDespawn()
    {
        CancelMonsterTasks();
    }


    protected NodeState CheckIsDead()
    {
        return (Stat.HealthComp.CurrentHp <= 0) ? NodeState.Success : NodeState.Failure;
    }

    protected virtual NodeState HandleDeadState()
    {
        // 1. 중복 실행 방지
        if (_state == MonsterState.Dead) return NodeState.Running;
        _state = MonsterState.Dead;

        // 2. 공통 로직: 콜라이더 끄기
        if (_col != null) _col.enabled = false;

        // 3. 공통 로직: 사망 애니메이션 재생
        if (_anim != null) _anim.CrossFade("Death", 0.1f);

        // 4. 공통 로직: 디스폰 타이머 시작 (GetDespawnDelay는 가상 메서드로 지연시간 결정)
        DespawnAsync(_monsterCts.Token, GetDespawnDelay()).Forget();

        return NodeState.Running;
    }


    protected virtual float GetDespawnDelay()
    {
        return 2.0f;
    }

    private async UniTaskVoid DespawnAsync(CancellationToken token, float delaySeconds)
    {
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        OnDespawned?.Invoke(this);
        Managers.Resource.Destroy(gameObject);
    }

    protected void CancelMonsterTasks()
    {
        if (_monsterCts != null)
        {
            _monsterCts.Cancel();
            _monsterCts.Dispose();
            _monsterCts = null;
        }
    }

    protected void CallOnDead()
    {
        OnDead?.Invoke();
    }

    // OnEnable, OnDisable, 사망 처리(HandleDeadState), CancellationToken 취소 로직 등 공통 요소 배치
    protected abstract void ConstructBehaviorTree();
}
