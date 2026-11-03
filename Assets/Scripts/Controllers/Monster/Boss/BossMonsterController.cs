using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using TMPro;
using UnityEngine;



public class BossMonsterController : BaseMonsterController, ISummonRegistry
{
    [Header("Boss Settings")]
    [SerializeField] private float _patternInterval = 2.0f; // 스킬 사이 딜레이
    [SerializeField] private float _totalHPLine = 300;
    [SerializeField] private Pattern _entrancePattern;

    protected BossMonsterPatternSelector _bmps;

    // 상태 관리 변수

    private float _nextPatternTime;

    protected override void Awake()
    {
        base.Awake();

        _bmps = GetComponent<BossMonsterPatternSelector>();
        _ss.PatternEnded += OnPatternEnded;
    }

    protected override void OnEnable()
    {
        base.OnEnable();    
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }

    // ====================================================
    // Behavior Tree Construction
    // ====================================================

    protected override void ConstructBehaviorTree()
    {
        // 사망은 BT 밖(Health.OnDead → OnDeath)
        _topNode = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { new ActionNode(ContinueRunning)}),
            new Sequence(new List<Node> { new ActionNode(WaitInterval)}),
            new Sequence(new List<Node> { new ActionNode(TryUseSkill)}),
        }); 
    }

   
    // ====================================================
    // Actions & Conditions
    // ====================================================

    private NodeState TryUseSkill()
    {
        // MonsterPatterSelector를 통해 하나 가져 와야됨.
        // 만약 선택된 패턴이 없다면 그냥 아무것도 안할거임. -> Idle

        _bmps.TryPick(CalcCurrentHPLine(), out Pattern pattern);

        if (pattern != null)
        {
            bool b = _ss.TryUseSkill(pattern, BuildContext());
            if (b)
            {
                return NodeState.Running;
            }
            
        }

        return NodeState.Failure;
    }

    private float CalcCurrentHPLine()
    {
        return Stat.HealthComp.CurrentHp * _totalHPLine / Stat.MaxHp.Value;
    }

    // 이미 실행중인 동작이 있다면 그거에 따라 Running이나 Success가 올텐데
    // Success면 이제 끝났다는거임.
    // 근데 Selector 노드에서 Success가 리턴되면 처음으로 돌아갈텐데..
    // 그러면 Tick의 결과가 Running이면 뒤에 노드들은 무시, Success면 끝났으니 다음 노드를 봐야됨.
    private void OnPatternEnded()
    {
        _nextPatternTime = Time.time + _patternInterval;
        _anim.CrossFade("Idle", 0.1f);
    }

    private NodeState ContinueRunning() => _ss.IsBusy ? NodeState.Running : NodeState.Failure;

    private NodeState WaitInterval() => Time.time < _nextPatternTime ? NodeState.Running : NodeState.Failure;


    // ====================================================
    // Override Base Methods
    // ====================================================

    // 보스 전용 사망 처리 (부모의 기능 호출 필수)
    // Health.OnDead가 생애에 한 번만 부른다 — 이후 Update는 IsDead라 BT를 더 돌리지 않는다.
    protected override void OnDeath()
    {
        // 패턴 끊기 · Death 애니 · 디스폰 타이머 (base) — 클리어 알림(CallOnDead)보다 먼저 끊어야
        // 알림 뒤에 시작될 수 있는 연출을 SS.Cancel이 멈추지 않는다.
        base.OnDeath();

        CallOnDead();

        CancelCombatToken();
    }

    public override Transform CurrentTarget
    {
        get
        {
            var c = Managers.Party.GetCurrentCharacter();
            return c != null ? c.transform : null;   
        }
    }



    // 보스는 죽는 연출이 기니까 좀 길게 (5초)
    protected override float GetDespawnDelay()
    {
        return 5.0f;
    }

    public void RegisterSummonedMonster(GameObject monster)
    {
        if (monster != null)
        {
            //_summonedMonsters.Add(monster);
        }
    }

    // ISummonRegistry — SummonEvent 가 소환한 몹을 여기로 등록
    public void RegisterSummoned(GameObject monster) => RegisterSummonedMonster(monster);

    public override void OnSpawn(SpawnContext ctx)
    {
        base.OnSpawn(ctx);   // 토큰 · SS 리셋이 여기서 → 등장 패턴 Context보다 먼저

        _bmps.ResetState();

        _ss.PlaySkill(_entrancePattern, BuildContext());
    }
}