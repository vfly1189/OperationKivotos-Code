using UnityEngine;
using UnityEngine.Playables;

public class BaseCharacter : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] protected float _speed = 5.0f;
    [SerializeField] protected Animator _anim;
    [SerializeField] protected float _attackRate = 0.5f;

    [Header("Skill Settings")]
    [SerializeField] protected PlayableDirector _skillTimeline;

    [Header("Combat Settings")]
    [SerializeField] protected GameObject _bulletPrefab;
    [SerializeField] protected Transform _firePoint;
    [SerializeField] protected ParticleSystem _fireEffectParticle;

    // 컴포넌트들
    public CharacterStat Stat { get; private set; }

    // 서브시스템들
    protected CharacterStateMachine _stateMachine;
    protected CharacterMovement _movement;
    protected CharacterCombat _combat;
    protected CharacterAnimationController _animController;

    public bool IsUsingSkill { get; protected set; }

    private void Awake()
    {
        Init();
    }

    public virtual void Init()
    {
        // 컴포넌트 초기화
        if (_anim == null) _anim = GetComponent<Animator>();
        Stat = GetComponent<CharacterStat>();
        if (Stat != null) Stat.Init();

        // 서브시스템 초기화
        _stateMachine = new CharacterStateMachine();
        _movement = new CharacterMovement(transform, _speed);
        _combat = new CharacterCombat(Stat, _attackRate);
        _animController = new CharacterAnimationController(_anim);

        // 이벤트 연결
        _stateMachine.OnStateChanged += OnStateChanged;

        if (_skillTimeline != null)
        {
            _skillTimeline.stopped += OnCutsceneEnded;
            _skillTimeline.Stop();
        }
    }

    void Start()
    {
        if (Stat != null)
        {
            Stat.OnDead -= HandleDeath;
            Stat.OnDead += HandleDeath;
        }
    }

    void Update()
    {
        if (_stateMachine.CurrentState == CharacterStateMachine.PlayerState.Attack)
        {
            _movement.RotateToMouse();
        }
    }

    void OnEnable()
    {
        if (Stat != null) Stat.OnDead += HandleDeath;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
    }

    protected virtual void OnDisable()
    {
        if (Stat != null) Stat.OnDead -= HandleDeath;
    }

    // ==================== Public API ====================

    public void Move(Vector2 dir)
    {
        if (!_stateMachine.CanMove() || Stat.IsDead) return;

        if (dir.sqrMagnitude > 0.01f)
        {
            if (_stateMachine.CurrentState != CharacterStateMachine.PlayerState.Move)
                _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Move);

            _movement.Move(dir);
        }
    }

    public void StopMove()
    {
        if (_stateMachine.CurrentState == CharacterStateMachine.PlayerState.Move)
            _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
    }

    public void Attack(bool isPressing)
    {
        if (!isPressing) return;
        if (!_stateMachine.CanAttack() || IsUsingSkill || Stat.IsDead) return;
        if (!_combat.CanAttack) return;

        if (_combat.TryAttack())
        {
            _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Attack);
        }
    }

    public void UseSkill_Q()
    {
        if (!_stateMachine.CanUseSkill() || IsUsingSkill || Stat.IsDead) return;
        if (!_combat.TryUseSkillQ()) return;

        Debug.Log("Q 스킬 사용!");
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Q_Skill_CutScene);
    }

    public void UseSkill_E()
    {
        if (!_stateMachine.CanUseSkill() || IsUsingSkill || Stat.IsDead) return;
        if (!_combat.TryUseSkillE()) return;

        Debug.Log("E 스킬 사용!");
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.E_Skill);
    }

    public void Victory()
    {
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Victory);
    }

    // ==================== State Callbacks ====================

    private void OnStateChanged(CharacterStateMachine.PlayerState newState)
    {
        // 무적 처리
        Stat.IsInvincible = (newState == CharacterStateMachine.PlayerState.Q_Skill_CutScene ||
                            newState == CharacterStateMachine.PlayerState.Q_Skill ||
                            newState == CharacterStateMachine.PlayerState.Victory);

        // 애니메이션 재생
        _animController.PlayState(newState);

        // 스킬 진입 처리
        if (newState == CharacterStateMachine.PlayerState.Q_Skill_CutScene)
        {
            OnSkillEnter();
        }
    }

    protected void OnSkillEnter()
    {
        if (_skillTimeline != null)
        {
            IsUsingSkill = true;
            _skillTimeline.Play();
        }
        else
        {
            _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Q_Skill);
        }
    }

    protected virtual void OnCutsceneEnded(PlayableDirector director)
    {
        Debug.Log("컷신 종료 -> 스킬 액션 상태로 전환");
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Q_Skill);
    }

    private void HandleDeath()
    {
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Death);
    }

    // ==================== Animation Events ====================

    void ChangeToIdle()
    {
        if (_stateMachine.IsDead()) return;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
        IsUsingSkill = false;
    }

    void CheckAttackFinished()
    {
        if (_stateMachine.IsDead()) return;

        bool isAttackPressed = UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;

        if (isAttackPressed && _stateMachine.CurrentState == CharacterStateMachine.PlayerState.Attack)
        {
            _anim.Play("Attack_Ing", 0, 0f);
            return;
        }

        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
        IsUsingSkill = false;
    }

   


    public void OnAttackEvent(AudioClip sfx)
    {
        if (_stateMachine.CurrentState == CharacterStateMachine.PlayerState.Attack)
        {
            PerformAttackAction();
            Managers.Sound.Play(sfx, Define.Sound.Effect);
        }
    }

    

    public void OnPlaySoundEvent(AudioClip clip)
    {
        if (clip != null) Managers.Sound.Play(clip, Define.Sound.Effect);
    }

    // ==================== Virtual Methods ====================

    protected virtual void PerformAttackAction() { }
    protected virtual void PlayFireEffect()
    {
        if (_fireEffectParticle == null) return;
        _fireEffectParticle.Stop();
        _fireEffectParticle.Play();
    }

    protected virtual void FireOneBullet()
    {
        // 1. 풀링으로 총알 생성 (위치/회전은 총구 기준)
        GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);

        bulletObj.transform.position = _firePoint.position;
        // 캐릭터가 바라보는 방향 기준으로 회전
        bulletObj.transform.rotation = transform.rotation;
        // 2. 데미지 주입
        BulletController bulletScript = bulletObj.GetComponent<BulletController>();
        if (bulletScript != null && Stat != null)
        {
            bulletScript.Init(Stat.Attack.Value, this.gameObject);
        }
        PlayFireEffect();
    }

    protected virtual void PlaySFXOnly() { }
    protected virtual void PlaySFX() { }

    protected virtual void OnESkillEvent(AudioClip sfx) { }

}
