using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Playables;

public class BaseCharacter : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] protected float _speed = 5.0f;
    [SerializeField] protected float _attackRate = 0.5f;
    [SerializeField] protected Animator _anim;
    
    [Header("Skill Settings")]
    [SerializeField] protected PlayableDirector _skillTimeline;

    [Header("Combat Settings")]
    [SerializeField] protected GameObject _bulletPrefab;
    [SerializeField] protected Transform _firePoint;
    [SerializeField] protected ParticleSystem _fireEffectParticle;

    // 상태 & 컴포넌트들
    public CharacterStat Stat { get; private set; }
    public IInteractable CurrentInteractable { get; private set; }
    public bool IsUsingSkill { get; protected set; }

    protected CharacterStateMachine _stateMachine;
    protected CharacterMovement _movement;
    protected CharacterCombat _combat;
    protected CharacterAnimationController _animController;

    protected GameObject _gameCanvas;

    // --- 공통 VFX (Static) --- ( 공용으로 쓰는 오라들 )
    protected static GameObject _healingAuraPrefab;
    protected GameObject _healingAuraInstance;
    protected ParticleSystem _healingAuraParticle;


    #region 유니티 생명주기

    private void Awake()
    {
        Init();
    }

    public virtual void Init()
    {
        // 컴포넌트 초기화
        if (_anim == null) _anim = GetComponent<Animator>();

        Stat = GetComponent<CharacterStat>();
        Stat?.Init();

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

        // 공통 이펙트 최초 1회 로드
        if (_healingAuraPrefab == null)
        {
            _healingAuraPrefab = Addressables.LoadAssetAsync<GameObject>("Healing_Aura").WaitForCompletion();
            if (_healingAuraPrefab == null) Debug.LogError("Healing_Aura 로드 실패!");
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
        //공격중에는 마우스 따라 공격
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

    #endregion

    #region 외부에서 호출될 API

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

    #endregion

    #region 콜백 함수들
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

            if(_gameCanvas == null)
            {
                GameSceneCanvas existingUI = FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
                if (existingUI != null)
                {
                    existingUI.gameObject.SetActive(false);
                    _gameCanvas = existingUI.gameObject;
                }
            }
        }
        else
        {
            _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Q_Skill);
        }
    }

    protected virtual void OnCutsceneEnded(PlayableDirector director)
    {
        Debug.Log("컷신 종료 -> 스킬 액션 상태로 전환");
        if (_gameCanvas != null) _gameCanvas.SetActive(true);
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Q_Skill);
    }

    private void HandleDeath()
    {
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Death);
    }

    #endregion

    #region 애니메이션 이벤트

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
    #endregion

    #region 가상 함수들 ( override )

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
            Debug.Log($"데미지 : {Stat.Attack.Value}");
            bulletScript.Init(Stat.Attack.Value, this.gameObject);
        }
        PlayFireEffect();
    }

    protected virtual void PlaySFXOnly() { }
    protected virtual void PlaySFX() { }

    protected virtual void OnESkillEvent(AudioClip sfx) { }

    #endregion

    #region 충돌 & 상호작용

    // NPC의 Trigger Collider 영역에 들어갔을 때
    private void OnTriggerEnter(Collider other)
    {
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            CurrentInteractable = interactable;

            // 객체 구분 없이 다형성으로 호출 (힐링이든 UI든 해당 객체가 알아서 처리)
            interactable.OnTargetEnter(this);
        }
    }

    // NPC 영역에서 벗어났을 때
    private void OnTriggerExit(Collider other)
    {
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null && CurrentInteractable == interactable)
        {
            interactable.OnTargetExit(this);
            CurrentInteractable = null;
        }
    }

    // 힐링 효과 실행 메서드 
    public virtual void PlayHealingAura()
    {
        // 1. 인스턴스가 없다면 지연 생성 
        if (_healingAuraInstance == null && _healingAuraPrefab != null)
        {
            // 캐릭터의 발밑이나 특정 Transform을 부모로 설정하여 생성
            _healingAuraInstance = Instantiate(_healingAuraPrefab, transform);

            // 로컬 위치/회전 초기화
            _healingAuraInstance.transform.localPosition = Vector3.zero;
            _healingAuraInstance.transform.localRotation = Quaternion.identity;

            _healingAuraParticle = _healingAuraInstance.GetComponentInChildren<ParticleSystem>();
        }

        // 2. 이펙트 재생
        if (_healingAuraInstance != null)
        {
            _healingAuraInstance.SetActive(true);
            if (_healingAuraParticle != null)
            {
                _healingAuraParticle.Stop();
                _healingAuraParticle.Play();
            }
        }
    }

    // 힐링 효과 중지 메서드
    public virtual void StopHealingAura()
    {
        if (_healingAuraInstance != null && _healingAuraInstance.activeSelf)
        {
            if (_healingAuraParticle != null)
                _healingAuraParticle.Stop();

            // 완전히 끄려면 SetActive(false) 혹은 Particle이 끝나면 자동 소멸되도록 세팅
            _healingAuraInstance.SetActive(false);
        }
    }

    #endregion
}
