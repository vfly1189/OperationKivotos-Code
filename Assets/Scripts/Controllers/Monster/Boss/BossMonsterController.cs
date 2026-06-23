using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class BossMonsterController : BaseMonsterController
{
    [Header("Boss Settings")]
    [SerializeField] private float _patternInterval = 2.0f; // 스킬 사이 딜레이
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private List<TimelineAsset> _skills;

    // 상태 관리 변수
    private bool _isActionRunning = false;
    private bool _isSkillFiring = false;
    private bool _entranceFinish = false; // 오타 수정 (entrace -> entrance)
    private bool _isDeadProcessed = false;

    private List<GameObject> _summonedMonsters = new List<GameObject>();

    protected override void OnEnable()
    {
        base.OnEnable();

        // [핵심] 타임라인 종료 이벤트 구독
        if (_director != null)
            _director.stopped += OnTimelineStopped;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_director != null)
            _director.stopped -= OnTimelineStopped;
    }

    // ====================================================
    // Timeline Event Handler
    // ====================================================

    // [핵심] 타임라인 재생이 끝나면 자동으로 호출되어 다음 패턴으로 넘어가게 해줌
    private void OnTimelineStopped(PlayableDirector director)
    {
        if (_isActionRunning)
        {
            _isActionRunning = false;
            _isSkillFiring = true;

            // 상태를 Idle로 되돌려야 다음 BehaviorTree 루프가 제대로 작동함
            if (_state != MonsterState.Dead) _state = MonsterState.Idle;

            _anim.CrossFade("Idle", 0.2f);
        }
    }

    // 애니메이션 이벤트 등에서 수동 호출용 (평타 등 타임라인이 아닌 일반 애니메이션을 쓸 경우)
    public void OnAnimationFinished()
    {
        if (_isActionRunning)
        {
            _isActionRunning = false;

            if (_state != MonsterState.Dead) _state = MonsterState.Idle;

            _anim.CrossFade("Idle", 0.5f);
        }
    }

    public void OnEntranceFinish()
    {
        _entranceFinish = true;
        if (_state != MonsterState.Dead) _state = MonsterState.Idle;
        _anim.CrossFade("Idle", 1.0f);
    }

    // ====================================================
    // Behavior Tree Construction
    // ====================================================

    protected override void ConstructBehaviorTree()
    {
        // 0. 사망 노드 (부모의 공통 로직 활용)
        Node deadSequence = new Sequence(new List<Node>
        {
            new ActionNode(CheckIsDead),
            new ActionNode(HandleDeadState)
        });

        // 1. 전투 패턴 (이미 입장이 끝난 경우에만 실행)
        // [수정] 보스 전용 BossSequence 혹은 일반 Sequence 사용 (기존 BossSequence 유지)
        Node combatSequence = new BossSequence(new List<Node>
        {
            new ActionNode(CheckEntranceFinished),
            new RandomNode(new List<Node>
            {
                new ActionNode(() => UseSkill(0)),
                new ActionNode(() => UseSkill(1)),
                new ActionNode(() => UseSkill(2)),
                new ActionNode(() => UseSkill(3)),
                new ActionNode(() => UseSkill(4)),
            }),
            new WaitNode(_patternInterval)
        });

        // 최상위 Selector
        _topNode = new Selector(new List<Node>
        {
            deadSequence,
            combatSequence
        });
    }

    // ====================================================
    // Actions & Conditions
    // ====================================================

    private NodeState CheckEntranceFinished()
    {
        return _entranceFinish ? NodeState.Success : NodeState.Failure;
    }

    private NodeState UseAttack()
    {
        if (_isActionRunning) return NodeState.Running;

        if (_isSkillFiring)
        {
            _isSkillFiring = false;
            return NodeState.Success;
        }

        _isSkillFiring = true;
        _isActionRunning = true;
        _state = MonsterState.Attacking;

        _anim.CrossFade("Attack_Start", 0.1f);

        return NodeState.Running;
    }

    private NodeState UseSkill(int skillIdx)
    {
        if (_isActionRunning) return NodeState.Running;

        if (_isSkillFiring)
        {
            _isSkillFiring = false;
            return NodeState.Success;
        }

        _isSkillFiring = true;
        _isActionRunning = true;
        _state = MonsterState.Attacking; // [수정] 보스도 공격 중임을 상태로 표시

        if (skillIdx < _skills.Count && _skills[skillIdx] != null)
        {
            _director.Stop();
            _director.playableAsset = _skills[skillIdx];
            _director.RebindPlayableGraphOutputs();
            _director.time = 0;
            _director.Evaluate();
            _director.Play();
        }
        else
        {
            GameLog.LogWarning($"스킬 타임라인이 없습니다! Index: {skillIdx}");
            _isActionRunning = false;
            _isSkillFiring = false;
            if (_state != MonsterState.Dead) _state = MonsterState.Idle;
            return NodeState.Failure;
        }

        return NodeState.Running;


    }

    // ====================================================
    // Override Base Methods
    // ====================================================

    // 보스 전용 사망 처리 (부모의 기능 호출 필수)
    protected override NodeState HandleDeadState()
    {
        //// 중복 방지는 부모(Base)에서 처리하므로 여기선 제외 가능하거나 _state 체크
        //if (_state == MonsterState.Dead) return NodeState.Running;

        //// [핵심] 보스가 죽을 때 소환된 잡몹들도 모두 파괴 (또는 데미지를 줘서 죽게 만듦)
        //foreach (var minion in _summonedMonsters)
        //{
        //    if (minion != null)
        //    {
        //        // 방법 A: 즉시 파괴
        //        // Managers.Resource.Destroy(minion);

        //        // 방법 B: 잡몹도 죽는 애니메이션을 재생하게 하려면 Stat의 체력을 0으로 만듦
        //        var stat = minion.GetComponent<MonsterStat>();
        //        if (stat != null && !stat.IsDead)
        //        {
        //            stat.TakeDamage(new DamageInfo { Amount = 99999f, Attacker = this.gameObject });
        //        }
        //    }
        //}
        //_summonedMonsters.Clear(); // 리스트 비우기

        //// 1. 진행 중인 타임라인/스킬 강제 종료
        //if (_director != null && _director.state == PlayState.Playing)
        //{
        //    _director.Stop();
        //}

        //CallOnDead();

        //// 2. 부모의 HandleDeadState()를 호출 (Collider off, Anim Play, Event Invoke, Destroy 대기)
        //return base.HandleDeadState();

        // 이미 죽은 상태 처리를 마쳤다면 Success
        if (_isDeadProcessed) return NodeState.Success;

        _isDeadProcessed = true;
        // [추가] 시전 중이던 타임라인 스킬 즉시 강제 종료
        if (_director != null && _director.state == PlayState.Playing)
        {
            _director.Stop();
        }

        // 플래그 리셋 (다음 노드나 혹시 모를 부활에 대비)
        _isActionRunning = false;
        _isSkillFiring = false;

        CallOnDead();

        // 기본 사망 로직 실행 (애니메이션, 드롭 등)
        base.HandleDeadState();

        return NodeState.Success;
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
            _summonedMonsters.Add(monster);
        }
    }

}