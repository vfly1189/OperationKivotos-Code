using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using static MonsterController;

public class BossMonsterController : MonoBehaviour
{
    [Header("Boss Settings")]
    [SerializeField] private float _patternInterval = 2.0f; // 스킬 사이 딜레이

    [SerializeField] private PlayableDirector _director;
    [SerializeField] private List<TimelineAsset> _skills;

    [SerializeField] private GameObject[] _relics;

    // [추가] 사망 처리를 위한 변수
    private bool _isDeadProcessed = false;

    private Node _topNode;
    //private bool _hasEntered = false; // 입장 했는지 체크
    private bool _isActionRunning = false; // 현재 어떤 행동(애니메이션 등)이 진행 중인가?
    private bool _entraceFinish = false;
    private Animator _anim;

    public MonsterStat Stat { get; private set; }

    public event Action OnDead;

    // 공격 상태용
    //private int _skillIndex = -1;
    private bool _isSkillFiring = false; // 현재 스킬 로직이 진행 중인지 체크
    private void Awake()
    {
        Stat = GetComponent<MonsterStat>();
        if (Stat != null) Stat.Init();

        _anim = GetComponent<Animator>();
        ConstructBehaviorTree();
    }

    private void Update()
    {
        if (_topNode != null)
            _topNode.Evaluate();
    }

    private void OnEnable()
    {
        // 타임라인 종료 이벤트 구독
        if (_director != null)
            _director.stopped += OnTimelineStopped;
    }

    private void OnDisable()
    {
        // 타임라인 종료 이벤트 해제
        if (_director != null)
            _director.stopped -= OnTimelineStopped;
    }

    public GameObject[] GetRelic() => _relics;

    // ====================================================
    // Timeline Event Handler
    // ====================================================

    // 타임라인 재생이 끝나면 자동으로 호출됨
    private void OnTimelineStopped(PlayableDirector director)
    {
        // 타임라인 행동 종료 처리
        if (_isActionRunning)
        {
            _isActionRunning = false;
            // 타임라인 끝난 후 Idle로 돌아가기 (필요 시)
            _anim.CrossFade("Idle", 0.2f); // 타임라인 마지막 프레임이 Idle과 블렌딩되게 세팅했다면 생략 가능
        }
    }

    private void ConstructBehaviorTree()
    {
        // 0. 사망 노드 [추가됨]
        // (가장 최우선으로 평가: 죽었으면 아무것도 하지 말고 죽는 연출만 해라)
        Node deadSequence = new Sequence(new List<Node>
        {
            new ActionNode(CheckIsDead),
            new ActionNode(HandleDeadState)
        });


        // A. 전투 패턴 (이미 입장이 끝난 경우에만 실행)
        Node combatSequence = new BossSequence(new List<Node>
        {
            new ActionNode(CheckEntranceFinished), // 입장이 끝났나? (안끝났으면 실패 -> B로 넘어감)                          // 
            new RandomSelector(new List<Node>
            {
                new ActionNode(() => UseSkill(0)),    // 스킬 1 (Timeline)
                new ActionNode(() => UseSkill(1)),    // 스킬 2 (Timeline)
                new ActionNode(() => UseSkill(2)),    // 스킬 3 (Timeline) - 필요시 인덱스 조정
                new ActionNode(() => UseSkill(3)),    // 스킬 3 (Timeline) - 필요시 인덱스 조정
                new ActionNode(() => UseSkill(4))     // 평타
            }),
            new WaitNode(_patternInterval)
        });

        // 최상위 Selector: 전투 가능한지 보고 -> 안되면 입장 체크 -> 안되면 대기
        _topNode = new Selector(new List<Node>
        {
            deadSequence,
            combatSequence
        });
    }

    // ====================================================
    // Actions & Conditions
    // ====================================================

    // 조건: 이미 입장했는가?
    private NodeState CheckEntranceFinished()
    {
        // 입장이 끝났으면 Success -> 전투 패턴 진행
        // 입장이 안 끝났으면 Failure -> 입장 패턴 확인하러 감
        
        return _entraceFinish ? NodeState.Success : NodeState.Failure;
    }

    // [평타] Animator 사용
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

        // 평타 애니메이션 재생
        _anim.CrossFade("Attack_Start", 0.1f);

        return NodeState.Running;
    }

    // 행동: 스킬 사용 (수정됨)
    private NodeState UseSkill(int skillIdx)
    {
        // 1. 이미 재생 중 (Animator든 Timeline이든)
        if (_isActionRunning) return NodeState.Running;

        // 2. 방금 종료됨 (stopped 이벤트나 OnAnimationFinished가 _isActionRunning을 false로 만든 직후)
        if (_isSkillFiring)
        {
            _isSkillFiring = false;
            return NodeState.Success;
        }

        // 3. 시작
        _isSkillFiring = true;
        _isActionRunning = true;

        // 타임라인 재생 로직
        if (skillIdx < _skills.Count && _skills[skillIdx] != null)
        {
            _director.Stop(); // 안전장치
            _director.playableAsset = _skills[skillIdx];

            // 바인딩 갱신 (트랙이 바뀌면서 오브젝트 연결이 끊길 수 있음 -> 보통 Animator는 자동 연결되나, 
            // 커스텀 트랙이 있다면 여기서 SetGenericBinding 필요할 수 있음)
            _director.RebindPlayableGraphOutputs();

            _director.time = 0;
            _director.Evaluate(); // 첫 프레임 강제 적용 (튀는 현상 방지)
            _director.Play();
        }
        else
        {
            Debug.LogWarning($"스킬 타임라인이 없습니다! Index: {skillIdx}");
            _isActionRunning = false; // 즉시 종료 처리
            _isSkillFiring = false;
            return NodeState.Failure;
        }

        return NodeState.Running;
    }

    // [추가] 사망 체크
    private NodeState CheckIsDead()
    {
        if (Stat.CurrentHp <= 0)
        {
            return NodeState.Success;
        }
        return NodeState.Failure;
    }

    // [추가] 사망 처리 로직
    private NodeState HandleDeadState()
    {
        if (_isDeadProcessed) return NodeState.Running; // 사라질 때까지 계속 Running

        _isDeadProcessed = true;
        // Debug.Log("Boss Dead Logic Start");

        // 1. 진행 중인 타임라인/스킬 강제 종료
        if (_director != null && _director.state == PlayState.Playing)
        {
            _director.Stop();
        }

        // 2. 콜라이더 끄기 (시체에 딜 안 박히게)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 3. 사망 애니메이션 재생
        _anim.CrossFade("Death", 0.0f);
        OnDead?.Invoke();
        // 6. 3초 뒤 오브젝트 삭제 (연출 시간 확보)
        DespawnAsync(this.GetCancellationTokenOnDestroy()).Forget();

        return NodeState.Running;
    }

    //private IEnumerator CoDespawn()
    //{
    //    yield return new WaitForSeconds(5.0f); // 보스는 죽는 연출이 기니까 좀 길게

    //    // 보스전 종료 처리 (UI 띄우기 등)
    //    Debug.Log("보스 사망 연출 종료 -> 던전 클리어");
    //    Managers.Resource.Destroy(gameObject);
    //    OnDead?.Invoke();
    //}

    private async UniTaskVoid DespawnAsync(System.Threading.CancellationToken token)
    {
        // 보스는 죽는 연출이 기니까 좀 길게 (취소 시 조용히 넘김)
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(5.0f), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        Debug.Log("보스 사망 연출 종료 -> 던전 클리어");
        Managers.Resource.Destroy(gameObject);
 
    }

    // 애니메이션 이벤트 등에서 호출
    public void OnAnimationFinished()
    {
        _isActionRunning = false;
        _anim.CrossFade("Idle", 0.5f);
        // 여기서 NodeState를 Success로 바꿔줄 방법이 필요함.
        // ActionNode 구조상, 외부 변수(_isActionRunning)를 보고 
        // 다음 프레임 Evaluate()에서 Success를 리턴하게 설계해야 함.
    }
    public void OnEntranceFinish()
    {
        _entraceFinish = true;
        _anim.CrossFade("Idle", 1.0f);
    }
}
