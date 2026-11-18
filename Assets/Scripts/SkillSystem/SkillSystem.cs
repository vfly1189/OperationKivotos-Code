
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;



// 얘는 사실상 메인 창구
// 요청에 대해 수행할지?말지를? 결정 지어주는 자리.
public class SkillSystem : MonoBehaviour
{
    private Animator _animator;
    private BaseStat _stat;   // 몬스터 · 캐릭터 공용

    private NodeState _state = NodeState.Failure;

    //태그 목록
    GameplayTagContainer _tagContainer = new GameplayTagContainer();

    private readonly HashSet<Pattern> _granted = new HashSet<Pattern>();
    public Dictionary<Pattern, float> _patternLastUsedTime = new Dictionary<Pattern, float>();
    // 쿨 길이를 부르는 쪽이 정한 패턴 (플레이어 Q · E = 캐릭터 데이터의 쿨). 없으면 Pattern._coolDown
    private readonly Dictionary<Pattern, float> _cooldownOverride = new Dictionary<Pattern, float>();

    private PatternRunner _runner = new PatternRunner();
    //Effect 목록

    public event Action PatternEnded;

    public void Awake()
    {
        _animator = GetComponent<Animator>();
        _stat = GetComponent<BaseStat>();

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

    // beginNow = 첫 동작을 이 자리에서 시작 — 같은 프레임에 들어올 신호를 놓치지 않게 (플레이어 입력 · 애니 비트)
    public void PlaySkill(Pattern pattern, PatternContext ctx, bool beginNow = false)
    {
        _runner.Start(pattern, ctx);
        if (beginNow && _runner.IsBusy && _runner.Tick() == NodeState.Success) PatternEnded?.Invoke();
    }

    // 게이트만 — 쿨 검사 + 쿨 시작. 실행은 PlaySkill이 따로 (입력 시점에 쿨을 걸고 실행은 나중인 플레이어 Q)
    public bool TryCommit(Pattern pattern, float cooldown)
    {
        if (pattern == null || !CheckIsCoolDown(pattern) || !CheckCurrentTag()) return false;
        _cooldownOverride[pattern] = cooldown;
        _patternLastUsedTime[pattern] = Time.time;
        return true;
    }

    private float CooldownOf(Pattern pattern)
        => _cooldownOverride.TryGetValue(pattern, out float cd) ? cd : pattern._coolDown;

    //true면 사용가능, false면 사용 불가능
    public bool CheckIsCoolDown(Pattern pattern)
    {
        if (!_patternLastUsedTime.TryGetValue(pattern, out float last)) return true;
        return last + CooldownOf(pattern) < Time.time;
    }

    // UI 폴링용 — 절대시각이라 꺼져 있던 동안(캐릭터 교체)에도 흐른다
    public float CooldownRemaining(Pattern pattern)
    {
        if (pattern == null || !_patternLastUsedTime.TryGetValue(pattern, out float last)) return 0f;
        return Mathf.Max(0f, last + CooldownOf(pattern) - Time.time);
    }

    public float CooldownDuration(Pattern pattern)
        => pattern != null && _patternLastUsedTime.ContainsKey(pattern) ? CooldownOf(pattern) : 0f;

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
        _cooldownOverride.Clear();
    }
}
