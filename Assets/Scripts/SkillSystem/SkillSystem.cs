
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;



// 얘는 사실상 메인 창구
// 요청에 대해 수행할지?말지를? 결정 지어주는 자리.
public class SkillSystem : MonoBehaviour
{
    private Animator _animator;
    private MonsterStat _stat;

    private NodeState _state = NodeState.Failure;

    //태그 목록
    GameplayTagContainer _tagContainer = new GameplayTagContainer();

    private readonly HashSet<Pattern> _granted = new HashSet<Pattern>();
    public Dictionary<Pattern, float> _patternLastUsedTime = new Dictionary<Pattern, float>();

    private PatternRunner _runner = new PatternRunner();
    //Effect 목록

    public event Action PatternEnded;

    public void Awake()
    {
        _animator = GetComponent<Animator>();
        _stat = GetComponent<MonsterStat>();

    }

    public void Update()
    {
        if (!_runner.IsBusy) return;

        if (_runner.Tick() == NodeState.Success) PatternEnded?.Invoke();
    }


    public bool TryUseSkill(Pattern pattern, PatternContext ctx)
    {
        // 쿨타임 체크 || 태그 체크
        if(!CanUse(pattern)) return false;
        

        //만약에 사용가능하다면
        PlaySkill(pattern, ctx);
        _patternLastUsedTime[pattern] = Time.time;

        return true;
    }

    public void PlaySkill(Pattern pattern, PatternContext ctx)
    {
        _runner.Start(pattern, ctx);
    }

    //true면 사용가능, false면 사용 불가능
    public bool CheckIsCoolDown(Pattern pattern)
    {
        if (!_patternLastUsedTime.TryGetValue(pattern, out float last)) return true;
        return last + pattern._coolDown < Time.time;
    }

    //나중에 태그에 Stunned , Casting .. 이런 상태라면 false 처리
    //일단은 true
    public bool CheckCurrentTag()
    {
        return true;
    }

    public void SendTag(GameplayTagSO tag)
    {
        _runner.OnEvent(tag);
    }

    public void Grant(Pattern p) => _granted.Add(p);
    public bool CanUse(Pattern p) => _granted.Contains(p) && CheckIsCoolDown(p) && CheckCurrentTag();
    public bool IsBusy => _runner.IsBusy;
    public Pattern Current => _runner.Current;

    public NodeState NodeState => _state;

    // 사망 · 회수 전용 — 패턴은 원자 단위라 이 외에는 끊지 않는다.
    // 끊긴 패턴은 PatternEnded를 보내지 않는다(PatternEnded = 정상 종료).
    public void Cancel()
    {
        _runner.Cancel();
    }

    // 새 생애 — 풀 재사용 시 이전 생애의 쿨 기록이 남지 않게
    public void ResetState()
    {
        _patternLastUsedTime.Clear();
    }
}
