using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


// 보스 몬스터, 일반 몬스터 모두 공통으로 쓰는 기능들이 있어야됨.
// 공통 인것들
// Awake OnEnable OnDisable Update
// 사망 처리 (Health.OnDead)


// 보스몹 - 입장연출 , 사망연출이 존재
// 일반몹 -  

public abstract class BaseMonsterController : MonoBehaviour, IMonsterLifecycle, ITargetSource
{
    #region Components
    protected Animator _anim;
    private MonsterStat _stat;
    protected Health _health;
    protected Collider _col;
    protected SkillSystem _ss;
    private IAnchorSet _sockets;

    // 살았나 — Health가 유일한 출처
    protected bool IsDead => _health != null && _health.IsDead;


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
    protected CancellationTokenSource _monsterCombatCts;

    protected bool IsSpawned => _monsterCts != null;

    protected Transform _target;

    public virtual Transform CurrentTarget => _target != null && _target.gameObject.activeInHierarchy ? _target : null;

    // 교전 중인가 — 스포너가 꺼져도 회수를 미룰지 판단한다(SpawnerManager 교전 유지). 리쉬가 없는 몹은 교전이 안 끝날 수 있으니 기본 false.
    public virtual bool IsEngaged => false;

    public event Action OnDead;         // 체력이 0이 되는 순간
    public event Action<BaseMonsterController> OnDespawned;    // 시체 연출까지 끝나고 오브젝트가 사라지기 직전 (스포너 리스폰용)

    // 끄기 실험(갱신 LOD 0단계) — 측정 전용. 이번 생애에 꺼 둔 비용 항목. OnSpawn이 None으로 되돌린다.
    protected MonsterCostOff _costOff;
    protected bool IsOff(MonsterCostOff item) => (_costOff & item) != 0;

    // 갱신 LOD — 화면 밖이면서 교전 중이 아닌 몹은 '줄임' 상태로 덜 돌린다. SpawnerManager가 매 프레임 정하고, 다음 생애 OnSpawn이 원래대로.
    //  행동 트리 = interval 프레임에 한 번(phase로 몹마다 엇갈림) · Animator = 끔(자세가 굳음) · HP바 = 끔(Normal)
    //  교전 중이면 줄이지 않는다 — 화면 밖에서 쏘는 몹의 공격 타이밍이 늦어지면 안 되므로.
    //  Animator를 통째로 꺼도 되는 이유: 애니 신호(발사 · 공격 끝 · 장전 끝)가 필요한 건 공격 중인 몹인데, 공격 중이면 교전이라 줄이지 않는다.
    //  (10-13 측정: 뼈 계산만 생략하는 CullUpdateTransforms로는 줄임 몹 1마리당 −1.2μs뿐이라 판정 비용에 묻혔다)
    public bool IsReduced { get; private set; }
    private int _btInterval = 1;
    private int _btPhase;

    // Awake - OnEnable - Start - FixedUpdate - OnTrigger - OnCollision - Update - LateUpdate - OnDisable - Destroy
    protected virtual void Awake()
    {
        // 생애(OnSpawn · OnDespawn)는 TryGetComponent로 컨트롤러 하나에만 온다 — 둘이면 꺼져 있어도 앞쪽이 받는다
        if (GetComponents<BaseMonsterController>().Length > 1)
            Debug.LogError($"[{name}] 몬스터 컨트롤러가 둘 이상 — 하나만 남길 것 (생애 호출은 앞쪽 하나만 받는다)", this);

        if (_ss == null) _ss = GetComponent<SkillSystem>();
        _sockets = GetComponent<IAnchorSet>();

        if (_anim == null) _anim = GetComponent<Animator>();
        if (_col == null ) _col = GetComponent<Collider>();
        // 갱신 LOD가 Animator를 껐다 켤 때 상태가 기본 상태(Appear)로 돌아가 등장 연출이 다시 재생되지 않게.
        //  풀 재사용은 OnEnable의 Rebind가 따로 초기화하므로 영향 없다.
        if (_anim != null) _anim.keepAnimatorStateOnDisable = true;


        var tempStat = Stat;
        // 사망은 BT가 매 프레임 묻지 않고 Health 이벤트로 받는다 (BaseCharacter와 같은 방식)
        _health = tempStat.HealthComp;
        _health.OnDead += OnDeath;
        // 보상 지급 바인딩
        MonsterReward.EnsureOn(gameObject, tempStat);
        // 데미지 표시 바인딩
        DamageNumberPresenter.EnsureOn(gameObject, tempStat);


        ConstructBehaviorTree(); // 자식에서 구현
    }

    protected virtual void OnEnable() 
    {
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
        if (IsDead || _topNode == null)
            return;

        if (IsOff(MonsterCostOff.BehaviorTree)) return;   // 끄기 실험
        if (_btInterval > 1 && (Time.frameCount + _btPhase) % _btInterval != 0) return;   // 갱신 LOD

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
        _monsterCombatCts = new CancellationTokenSource();

        OnDead = null;
        OnDespawned = null;

        Stat?.HealthComp.Recover();
        _target = null;

        _ss?.ResetState();   // 새 생애 — 이전 생애의 쿨 기록이 남지 않게

        _topNode.Reset();

        // 끄기 실험 원복 — 풀 재사용 시 이전 생애 설정이 남지 않게. OnEnable(Rebind)보다 먼저 불린다.
        _costOff = MonsterCostOff.None;
        if (_anim != null) _anim.enabled = true;

        // 갱신 LOD 원복 — 줄임 상태로 회수됐다 재사용될 수 있다
        IsReduced = false;
        _btInterval = 1;
    }

    public void SetReduced(bool reduced, int btInterval, int btPhase)
    {
        if (IsReduced == reduced) return;
        IsReduced = reduced;
        _btInterval = reduced ? Mathf.Max(1, btInterval) : 1;
        _btPhase = btPhase;
        if (_anim != null && !IsOff(MonsterCostOff.Animator)) _anim.enabled = !reduced;
        OnReducedChanged(reduced);
    }

    protected virtual void OnReducedChanged(bool reduced) { }

    // 끄기 실험 적용 — SpawnerManager가 스폰 직후(켜진 뒤) 부른다. 하위 클래스가 자기 항목(HP바 · 회피)을 더한다.
    public virtual void ApplyCostOff(MonsterCostOff off)
    {
        _costOff = off;
        if (IsOff(MonsterCostOff.Animator) && _anim != null) _anim.enabled = false;
    }

    // 모든 회수가 지나는 합류점 — 죽지 않고 회수돼도 러너가 이전 패턴을 들고 있지 않게
    public virtual void OnDespawn()
    {
        _ss?.Cancel();
        CancelMonsterTasks();
    }

    // SS에 넘길 실행 정보 — 몬스터면 누구나 같다(시전자 · 타겟 출처 · 소켓 · 스탯 · 생애 토큰)
    protected PatternContext BuildContext()
    {
        return new PatternContext
        {
            owner = _ss,
            targets = this,
            sockets = _sockets,
            cToken = _monsterCombatCts != null ? _monsterCombatCts.Token : new CancellationToken(true),
            stat = Stat
        };
    }

    protected void CancelCombatToken()
    {
        if(_monsterCombatCts != null)
        {
            _monsterCombatCts.Cancel();
            _monsterCombatCts.Dispose();
            _monsterCombatCts = null;
        }
    }

    // 사망 — Health.OnDead 구독. 생애에 한 번만 온다(Health.Die가 IsDead로 막음) → 중복 방지 불필요.
    // 피격 호출 안에서 동기로 불린다. 이후 Update는 IsDead라 BT를 돌리지 않는다.
    protected virtual void OnDeath()
    {
        // 시전 중이던 패턴 끊기 — Death 크로스페이드보다 먼저: 타임라인이 Animator를 놓은 뒤에 Death가 들어가야 한다.
        _ss?.Cancel();

        if (_col != null) _col.enabled = false;
        if (_anim != null) _anim.CrossFade("Death", 0.1f);

        // 디스폰 타이머 (GetDespawnDelay는 가상 메서드로 지연시간 결정)
        if (_monsterCts != null) DespawnAsync(_monsterCts.Token, GetDespawnDelay()).Forget();
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

        CancelCombatToken();
    }

    protected void CallOnDead()
    {
        OnDead?.Invoke();
    }

    // OnEnable, OnDisable, 사망 처리(OnDeath), CancellationToken 취소 로직 등 공통 요소 배치
    protected abstract void ConstructBehaviorTree();
}
