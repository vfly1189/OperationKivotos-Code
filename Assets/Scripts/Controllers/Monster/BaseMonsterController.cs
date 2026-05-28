using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using static MonsterController;

// 보스 몬스터, 일반 몬스터 모두 공통으로 쓰는 기능들이 있어야됨.
// 공통 인것들
// Awake OnEnable OnDisable Update
// CheckIsDead


// 보스몹 - 입장연출 , 사망연출이 존재
// 일반몹 -  

public abstract class BaseMonsterController : MonoBehaviour
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

    [SerializeField] protected Animator _anim;

    private MonsterStat _stat;

    // 외부에서 Stat을 요구할 때, 만약 비어있다면 스스로 찾아오게 만듦
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

    protected MonsterState _state = MonsterState.Spawning;
    protected Node _topNode;
    protected CancellationTokenSource _monsterCts;

    public event Action OnDead;         // 체력이 0이 되는 순간
    public event Action OnDespawned;    // 시체 연출까지 끝나고 오브젝트가 사라지기 직전 (스포너 리스폰용)


    protected virtual void Start() { }
    protected virtual void Awake()
    {
        var tempStat = Stat;

        if (_anim == null) _anim = GetComponent<Animator>();

        ConstructBehaviorTree(); // 자식에서 구현
    }

    protected virtual void OnEnable() 
    {
        CancelMonsterTasks();
        _monsterCts = new CancellationTokenSource();
        _state = MonsterState.Spawning;

        Stat?.Recover();

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        if (_anim != null)
        {
            _anim.Rebind();
            _anim.Play("Appear");
        }
    }
    protected virtual void OnDisable()
    {
        CancelMonsterTasks();
    }
    protected virtual void OnDestroy() { }

    protected virtual void Update()
    {
        // 죽었거나 행동 트리가 없다면 실행하지 않음
        if (_state == MonsterState.Dead || _topNode == null)
            return;

        // 매 프레임 행동 트리 평가(실행)
        _topNode.Evaluate();
    }

    protected NodeState CheckIsDead()
    {
        return (Stat.CurrentHp <= 0) ? NodeState.Success : NodeState.Failure;
    }

    protected virtual NodeState HandleDeadState()
    {
        // 1. 중복 실행 방지
        if (_state == MonsterState.Dead) return NodeState.Running;
        _state = MonsterState.Dead;

        // 2. 공통 로직: 콜라이더 끄기
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 3. 공통 로직: 사망 애니메이션 재생
        if (_anim != null) _anim.CrossFade("Death", 0.1f);

        // 4. 공통 로직: 디스폰 타이머 시작 (GetDespawnDelay는 가상 메서드로 지연시간 결정)
        DespawnAsync(_monsterCts.Token, GetDespawnDelay()).Forget();

        return NodeState.Running;
    }


    protected virtual float GetDespawnDelay()
    {
        return 2.0f; // 일반 몬스터의 기본 디스폰 대기 시간 (2초)
    }

    private async UniTaskVoid DespawnAsync(CancellationToken token, float delaySeconds)
    {
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        OnDespawned?.Invoke();
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
