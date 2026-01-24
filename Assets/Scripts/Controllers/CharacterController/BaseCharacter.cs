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
        Skill_CutScene,     // 스킬 컷신
        Skill,              // 스킬 모션 (컷신 x)
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

        if(Stat == null)
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
        // 공격 중이나 스킬 중엔 이동 불가
        if (_state == PlayerState.Attack || _state == PlayerState.Skill || _state == PlayerState.Skill_CutScene)
            return;

        if (_state != PlayerState.Move)
            ChangeState(PlayerState.Move);

        // 실제 이동 로직
        Vector3 moveDir = new Vector3(dir.x, 0, dir.y).normalized;
        transform.position += moveDir * _speed * Time.deltaTime;

        // 회전 로직
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
        // 공격 키를 누르고 있는 동안
        if (isPressing)
        {
            // Idle이나 Move 상태에서만 공격 시작 가능
            if (_state == PlayerState.Idle || _state == PlayerState.Move || _state == PlayerState.Attack)
            {
                if (_state != PlayerState.Attack)
                    ChangeState(PlayerState.Attack);

                // 쿨타임 체크 및 공격 실행
                if (Time.time - _lastAttackTime > _attackRate)
                {
                    _lastAttackTime = Time.time;
                    PerformAttackAction();
                }
            }
        }
        else
        {
            // 손 떼면 Idle로 (연사형 무기가 아니라면)
            if (_state == PlayerState.Attack)
                ChangeState(PlayerState.Idle);
        }
    }

    public void UseSkill_Q()
    {
        if (_state == PlayerState.Idle || _state == PlayerState.Move || _state == PlayerState.Attack)
        {
            Debug.Log("스킬 사용!");
            ChangeState(PlayerState.Skill_CutScene);
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
            case PlayerState.Skill_CutScene:
                _anim.CrossFade("Q_Skill_CutScene", 0.0f); //컷씬도 굳이 블렌딩?
                OnSkillEnter();
                break;
            case PlayerState.Skill:
                _anim.CrossFade("Q_Skill", 0.0f);   //컷씬에서 다시 InGame 모션으로 갈때 굳이 블렌딩? 안해도 될듯
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
            _skillTimeline.Play(); // 재생
        }
        else
        {
            // 타임라인 없으면 바로 스킬 상태로
            ChangeState(PlayerState.Skill);
        }
    }

    // 타임라인 종료 콜백 (공통)
    protected virtual void OnCutsceneEnded(PlayableDirector director)
    {
        Debug.Log("컷신 종료 -> 스킬 액션 상태로 전환");
        ChangeState(PlayerState.Skill);
    }




    #region AnimationEvent
    //애니메이션 이벤트 콜백
    void ChangeToIdle()
    {
        ChangeState(PlayerState.Idle);
    }
    
    void PlaySFX()
    {
        PlaySFXOnly();
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
        int layerMask = LayerMask.GetMask("Wall");
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
