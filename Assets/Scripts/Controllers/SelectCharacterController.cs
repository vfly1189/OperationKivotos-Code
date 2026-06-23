using System.Collections;
using UnityEngine;

public class SelectCharacterController : MonoBehaviour
{
    private Animator _animator;

    // 상태 정의
    private enum State
    {
        None,
        Select_Start,   // 등장 모션 중
        Select_Idle     // 대기 모션 중
    }

    private State _currentState = State.None;
    //private float _stateTimer = 0f; // 현재 상태가 얼마나 지났는지 체크
    private float _currentAnimLength = 0f; // 현재 재생 중인 애니메이션의 길이

    private static readonly int Hash_SelectStart = Animator.StringToHash("Select_Start");
    private static readonly int Hash_SelectIdle = Animator.StringToHash("Select_Idle");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        // 켜질 때마다 시작
        ChangeState(State.Select_Start);
    }

    private void Update()
    {
        switch (_currentState)
        {
            case State.Select_Start:
                // 0번 레이어의 현재 상태 정보 가져오기
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);

                // 1. 현재 재생 중인 애니메이션이 'Select_Start'인지 확인 (해시값 비교)
                // 2. 진행률(normalizedTime)이 1.0 (100%)을 넘었는지 확인
                // 3. 현재 Transition(전환) 중이 아닐 때만 체크 (중복 실행 방지)
                if (info.shortNameHash == Hash_SelectStart &&
                    info.normalizedTime >= 0.99f &&
                    !_animator.IsInTransition(0))
                {
                    ChangeState(State.Select_Idle);
                }
                break;

            case State.Select_Idle:
                // 반복
                break;
        }
    }

    private void ChangeState(State newState)
    {
        _currentState = newState;
        //_stateTimer = 0f;

        switch (newState)
        {
            case State.Select_Start:
                _animator.CrossFade(Hash_SelectStart, 0.1f);
                // 중요: 클립 길이를 가져와서 저장 (못 가져오면 기본값 2초)
                _currentAnimLength = GetClipLength("Select_Start");
                if (_currentAnimLength == 0) _currentAnimLength = 2.0f;
                break;

            case State.Select_Idle:
                _animator.CrossFade(Hash_SelectIdle, 0.2f);
                break;
        }
    }

    // 애니메이션 클립 이름으로 길이 찾기 (초기화 비용 있음, 캐싱 권장)
    private float GetClipLength(string clipName)
    {
        if (_animator.runtimeAnimatorController == null) return 0f;

        foreach (var clip in _animator.runtimeAnimatorController.animationClips)
        {
            // 클립 이름이 정확히 일치하거나, 임포트 이름 문제로 끝부분이 일치하는 경우
            if (clip.name.EndsWith(clipName))
            {
                return clip.length;
            }
        }

        GameLog.LogWarning($"[SelectCharacterController] '{clipName}' 클립을 Animator에서 찾을 수 없습니다!");
        return 0f;
    }
}
