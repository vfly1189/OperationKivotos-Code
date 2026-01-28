using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class BaseCharacter : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Move,
        Attack,             // 기본 공격
        Q_Skill_CutScene,     // 스킬 컷신
        Q_Skill,              // 스킬 모션 (컷신 x)
        E_Skill,
        Die
    }

    [Header("Base Settings")]
    [SerializeField] protected float _speed = 5.0f;
    [SerializeField] protected Animator _anim;

    [Header("Common Skill Settings")]
    [SerializeField] protected PlayableDirector _skillTimeline; // 컷신용 타임라인

    [Header("Common Attack Settings")]
    [SerializeField] protected float _attackRate = 0.5f; // 공격 속도
    protected float _lastAttackTime = 0f;

    [Header("Sounds")]
    [SerializeField] protected AudioClip _sfx;

    // 현재 상태
    protected PlayerState _state = PlayerState.Idle;
    protected Rigidbody _rb; // 리지드바디 변수 추가
    // [추가] 충돌 체크를 위한 레이어 마스크 (원하는 장애물 레이어 이름 입력)
    private int _obstacleMask;
    public bool IsUsingSkill { get; protected set; } = false;

    public CharacterStat Stat { get; private set; }

    private void Awake()
    {
        Init();
    }

    void Start()
    {
        Init();
        Stat = GetComponent<CharacterStat>();
        if (_skillTimeline != null)
        {
            _skillTimeline.stopped += OnCutsceneEnded;
            _skillTimeline.Stop();
        }
    }
    public virtual void Init()
    {
        if (_anim == null) 
            _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
        // [추가] Wall이나 Block 레이어 등 막혀야 할 레이어를 설정하세요.
        // 예: LayerMask.GetMask("Wall", "Obstacle")
        // 여기선 임시로 Wall이 없으면 Default를 제외한 모든 것을 체크하도록 설정
        _obstacleMask = LayerMask.GetMask("Collider");
        if (_obstacleMask == 0) _obstacleMask = LayerMask.GetMask("Default");
        if (Stat == null)
        {
            Stat = GetComponent<CharacterStat>();
            if (Stat != null) Stat.Init();
        }
    }

    void Update()
    {
        if (_state == PlayerState.Attack)
        {
            RotateToMouse();
        }
    }


    public void Move(Vector2 dir)
    {
        //// 공격 중이나 스킬 중엔 이동 불가
        //if (_state == PlayerState.Attack 
        //    || _state == PlayerState.Q_Skill 
        //    || _state == PlayerState.Q_Skill_CutScene
        //    || _state == PlayerState.E_Skill)
        //    return;

        //if (_state != PlayerState.Move)
        //    ChangeState(PlayerState.Move);

        //// 실제 이동 로직
        //Vector3 moveDir = new Vector3(dir.x, 0, dir.y).normalized;
        //transform.position += moveDir * _speed * Time.deltaTime;

        //// 회전 로직
        //if (moveDir != Vector3.zero)
        //{
        //    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
        //    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10.0f * Time.deltaTime);
        //}


        // 공격 중이나 스킬 중엔 이동 불가
        if (_state == PlayerState.Attack
            || _state == PlayerState.Q_Skill
            || _state == PlayerState.Q_Skill_CutScene
            || _state == PlayerState.E_Skill)
            return;

        // 1. 상태 변경 (이동 애니메이션은 무조건 재생)
        if (_state != PlayerState.Move)
            ChangeState(PlayerState.Move);

        // 2. 이동 벡터 계산
        Vector3 moveDir = new Vector3(dir.x, 0, dir.y).normalized;
        float moveDist = _speed * Time.deltaTime;

        // 3. 충돌 체크 (Raycast)
        // 캐릭터 발밑(transform.position)보다는 살짝 위(0.5f)에서 쏴야 바닥에 안 걸립니다.
        Vector3 rayOrigin = gameObject.transform.position + Vector3.up * 0.5f;
        float checkDistance = moveDist + 0.5f; // 조금 더 길게 체크
        // "앞으로 moveDist만큼 + 약간의 여유(0.1f)를 두고 쏴서 벽이 있는지 확인"
        bool isHit = Physics.Raycast(rayOrigin, moveDir, out RaycastHit hit, moveDist + 0.5f, _obstacleMask);
        Debug.DrawRay(rayOrigin, moveDir * checkDistance, isHit ? Color.red : Color.green);

        // [핵심] 벽이 없으면 이동, 벽이 있으면 제자리 걸음 (이동 코드 건너뜀)
        if (!isHit)
        {
            transform.position += moveDir * moveDist;
        }
        // else { 벽에 부딪힘 -> 위치 이동은 안 하지만 _state는 Move 상태이므로 애니메이션은 계속 뜀 }

        // 4. 회전 로직 (벽에 막혀도 바라보는 방향은 입력한 쪽으로 돌아가는 게 자연스러움)
        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10.0f * Time.deltaTime);
        }
    }

    public void StopMove()
    {
        if (_state == PlayerState.Move)
            ChangeState(PlayerState.Idle);
    }

    public void Attack(bool isPressing)
    {
        if (isPressing)
        {
            // 공격 키를 누르면 그냥 '공격 상태'로만 전환
            // -> 애니메이션이 재생되면서 알아서 이벤트를 호출할 것임
            if (_state != PlayerState.Attack)
            {
                ChangeState(PlayerState.Attack);
            }
        }
        else
        {
            // 손 떼면 Idle로 복귀
            if (_state == PlayerState.Attack)
                ChangeState(PlayerState.Idle);
        }
    }

    public void OnAttackEvent(AudioClip sfx)
    {
        // 공격 상태일 때만 발사 (혹시 상태가 바뀌었는데 이벤트가 늦게 올 수 있으니 체크)
        if (_state == PlayerState.Attack)
        {
            PerformAttackAction();
            Managers.Sound.Play(sfx, Define.Sound.Effect);
        }
    }

    public void UseSkill_Q()
    {
        if (_state == PlayerState.Idle || _state == PlayerState.Move || _state == PlayerState.Attack)
        {
            if (Stat.TryUseSkillQ() == false)
                return;

            Debug.Log("E 스킬 사용!");

            ChangeState(PlayerState.Q_Skill_CutScene);
        }
    }
    public void UseSkill_E()
    {
        if (_state == PlayerState.Idle || _state == PlayerState.Move || _state == PlayerState.Attack)
        {
            if (Stat.TryUseSkillE() == false)
                return;

            Debug.Log("E 스킬 사용!");

            ChangeState(PlayerState.E_Skill);
        }
    }

    // 상태 변경 메서드 
    public void ChangeState(PlayerState newState)
    {
        if (_state == newState) return;

        _state = newState;

        // 상태 진입 시 애니메이션 자동 재생 (CrossFade 활용)
        switch (_state)
        {
            case PlayerState.Idle:
                _anim.CrossFade("Idle", 0.1f);
                break;
            case PlayerState.Move:
                _anim.CrossFade("Move", 0.1f);
                break;
            case PlayerState.Attack:
                _anim.CrossFade("Attack_Ing", 0.1f); // 자식마다 다른 모션일 경우 오버라이드 고려
                break;
            case PlayerState.Q_Skill_CutScene:
                _anim.CrossFade("Q_Skill_CutScene", 0.0f); //컷씬도 굳이 블렌딩?
                OnSkillEnter();
                break;
            case PlayerState.Q_Skill:
                _anim.CrossFade("Q_Skill", 0.0f);   //컷씬에서 다시 InGame 모션으로 갈때 굳이 블렌딩? 안해도 될듯
                break;
            case PlayerState.E_Skill:
                _anim.CrossFade("E_Skill", 0.0f);
                break;
                //case PlayerState.Die:
                //    _anim.CrossFade("Die", 0.1f);
                //    break;
        }
    }


    protected void OnSkillEnter()
    {
        if (_skillTimeline != null)
        {
            IsUsingSkill = true;
            
            _skillTimeline.Play(); // 재생
        }
        else
        {
            // 타임라인 없으면 바로 스킬 상태로
            ChangeState(PlayerState.Q_Skill);
        }
    }

    // 타임라인 종료 콜백 (공통)
    protected virtual void OnCutsceneEnded(PlayableDirector director)
    {
        Debug.Log("컷신 종료 -> 스킬 액션 상태로 전환");
        ChangeState(PlayerState.Q_Skill);
    }




    #region AnimationEvent
    //애니메이션 이벤트 콜백
    void ChangeToIdle()
    {
        ChangeState(PlayerState.Idle);
        IsUsingSkill = false;
    }
    
    void PlaySFX()
    {
        PlaySFXOnly();
    }

    public void OnPlaySoundEvent(AudioClip clip)
    {
        if (clip == null) return;

        // 사운드 매니저를 통해 재생
        Managers.Sound.Play(clip, Define.Sound.Effect);
    }

    #endregion


    #region Utility
    //유틸리티
    protected void RotateToMouse()
    {
        if (Camera.main == null) return;

        // New Input System 방식으로 마우스 위치 가져오기
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        // Ground 레이어(혹은 Wall이 아닌 레이어) 체크
        // "Ground" 레이어가 없으면 임시로 모든 레이어(-1) 체크
        int layerMask = LayerMask.GetMask("Map");
        if (layerMask == 0) layerMask = -1;

        if (Physics.Raycast(ray, out RaycastHit hit, 100.0f, layerMask))
        {
            Vector3 target = hit.point;
            target.y = transform.position.y; // 캐릭터 높이 유지
            transform.LookAt(target);
        }
    }
 

    #endregion


    #region virtual function
    protected virtual void PlaySFXOnly()
    {

    }
    protected virtual void PerformAttackAction() { }
    #endregion


}
