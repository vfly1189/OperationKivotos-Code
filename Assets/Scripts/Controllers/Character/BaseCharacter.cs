using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Playables;

public class BaseCharacter : MonoBehaviour, IAbilityCaster
{
    [Header("Base Settings")]
    [SerializeField] protected float _speed = 5.0f;
    [SerializeField] protected float _attackRate = 0.5f;
    [SerializeField] protected Animator anim;

    [Header("Skill Settings")]
    [SerializeField] protected PlayableDirector skillTimeline;

    [Header("Combat Settings")]
    //[SerializeField] protected GameObject _bulletPrefab;
    [SerializeField] protected Transform _firePoint;
    //[SerializeField] protected ParticleSystem fireEffectParticle;

    [SerializeField] protected List<AbilityData> _abilities = new List<AbilityData>();
    private readonly AbilityRunner _abilityRunner = new AbilityRunner();

    [Header("Role")]
    [SerializeField] protected RoleDataSO _roleData;          // 역할 (= E스킬을 결정)
    [SerializeField] protected AbilityData _eAbilityOverride; // 있으면 역할 기본 E를 덮어씀 (모델 1)
    protected AbilityData _eAbility;                          // 해석된 E (override ?? 역할 기본)

    public CharacterStat Stat { get; private set; }
    public IInteractable CurrentInteractable { get; private set; }
    public bool IsUsingSkill { get; protected set; }
    public bool CanSwap => _stateMachine.CanSwap; // 공격/스킬 중 스왑 차단용

    protected CharacterStateMachine _stateMachine;
    protected CharacterMovement _movement;
    protected CharacterCombat _combat;
    protected CharacterAnimationController _animController;
    protected GameObject _gameCanvas;

    // VFX
    protected static GameObject _healingAuraPrefab;
    protected GameObject _healingAuraInstance;
    protected ParticleSystem _healingAuraParticle;
    protected CancellationTokenSource _actionCts;

    public event Action<BaseCharacter> OnCharacterDead;

    #region Ability

    // 애니메이션 이벤트가 부르는 실제 발동 지점 (평타·반격 등).
    // 입력이 직접 부르지 않는다 — 발동 시점은 애니메이션 비트가 정한다.
    public void TryUseAbility(int id)
    {
        if (id < 0 || id >= _abilities.Count) return;
        TryCast(_abilities[id]);
    }

    // 어빌리티 데이터를 직접 캐스트 (E처럼 인덱스 없이 발동하는 경우에도 사용)
    protected void TryCast(AbilityData ability)
    {
        if (ability == null) return;
        if (Stat == null || Stat.HealthComp.IsDead) return;

        var token = _actionCts?.Token ?? CancellationToken.None;
        var ctx = new AbilityContext
        {
            Caster      = this,
            CasterGO    = gameObject,
            CasterStat  = Stat,
            Object      = _firePoint,   // 총구 = 스폰 기준 (aim은 캐릭터 회전이 firePoint에 반영됨)
            Target      = null,
            TargetPoint = transform.position + transform.forward,
        };
        _abilityRunner.TryCast(ability, ctx, token).Forget();
    }

    #endregion


    #region Lifecycle

    private void Awake() => Init();

    public virtual async void Init()
    {
        if (anim == null) anim = GetComponent<Animator>();
        Stat = GetComponent<CharacterStat>();
        Stat?.Init();

        // 데미지 표시(토스트) 책임을 Stat에서 분리 — DamageNumberPresenter 부착·바인딩
        DamageNumberPresenter.EnsureOn(gameObject, Stat);

        // E스킬 해석 (모델 1): 캐릭터 오버라이드가 있으면 그것, 없으면 역할 기본 E
        _eAbility = _eAbilityOverride != null ? _eAbilityOverride
                  : (_roleData != null ? _roleData.eAbility : null);

        _stateMachine = new CharacterStateMachine();
        _movement = new CharacterMovement(transform, _speed);
        _combat = new CharacterCombat(Stat, _attackRate);
        _animController = new CharacterAnimationController(anim);

        _stateMachine.OnStateChanged += OnStateChanged;

        if (skillTimeline != null)
        {
            skillTimeline.stopped += OnCutsceneEnded;
            skillTimeline.Stop();
        }

        
        //_healingAuraPrefab = Addressables.LoadAssetAsync<GameObject>("HealingAura").WaitForCompletion();
        _healingAuraPrefab = await Managers.Resource.LoadAsync<GameObject>("Healing_Aura", isGlobal: true);
        if (_healingAuraPrefab == null) GameLog.LogError("HealingAura 로드 실패!");
        
    }

    private void Update()
    {
        if (_stateMachine.CurrentState == CharacterStateMachine.PlayerState.Attack)
            _movement.RotateToMouse();
    }

    protected virtual void OnEnable()
    {
        if (Stat != null)
        {
            Stat.HealthComp.OnDead -= HandleDeath;
            Stat.HealthComp.OnDead += HandleDeath;
        }
        _stateMachine?.ChangeState(CharacterStateMachine.PlayerState.Idle);
    }

    protected virtual void OnDisable()
    {
        if (Stat != null) Stat.HealthComp.OnDead -= HandleDeath;
        CancelCurrentAction();
    }

    #endregion

    #region API

    public void Move(Vector2 dir)
    {
        if (!_stateMachine.CanMove || Stat.HealthComp.IsDead) return;

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
        if (!_stateMachine.CanAttack || IsUsingSkill || Stat.HealthComp.IsDead) return;
        if (!_combat.CanAttack) return;

        if (_combat.TryAttack())
            _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Attack);
    }

    public void UseSkillQ()
    {
        if (!_stateMachine.CanUseSkill || IsUsingSkill || Stat.HealthComp.IsDead) return;
        if (!_combat.TryUseSkillQ()) return;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.QSkillCutScene);
    }

    public void UseSkillE()
    {
        if (!_stateMachine.CanUseSkill || IsUsingSkill || Stat.HealthComp.IsDead) return;
        if (!_combat.TryUseSkillE()) return;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.E_Skill);

        // 역할이 부여한 E 어빌리티 발동 (게이트는 위 TryUseSkillE, 이펙트는 데이터 주도)
        TryCast(_eAbility);
    }

    public void Victory() =>
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Victory);

    public void ResetCharacterState(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);
        Stat?.ResetState();
        IsUsingSkill = false;
        _stateMachine?.ChangeState(CharacterStateMachine.PlayerState.Idle);
        anim?.Rebind();
    }

    // 씬 귀환 시 호출
    public void ReturnToTownCharacterState(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);

        // 일반 ResetState 대신 귀환용 보정 함수 호출
        if (Stat != null)
        {
            Stat.ReturnToTownState();
        }

        IsUsingSkill = false;
        _stateMachine?.ChangeState(CharacterStateMachine.PlayerState.Idle);
        anim?.Rebind();
    }

    #endregion

    #region State Callbacks

    private void OnStateChanged(CharacterStateMachine.PlayerState newState)
    {
        CancelCurrentAction();
        _actionCts = new CancellationTokenSource();

        Stat.HealthComp.IsInvincible = newState == CharacterStateMachine.PlayerState.QSkillCutScene
                         || newState == CharacterStateMachine.PlayerState.Q_Skill
                         || newState == CharacterStateMachine.PlayerState.Victory;

        _animController.PlayState(newState);

        if (newState == CharacterStateMachine.PlayerState.QSkillCutScene)
            OnSkillEnter();
    }

    protected void CancelCurrentAction()
    {
        if (_actionCts == null) return;
        _actionCts.Cancel();
        _actionCts.Dispose();
        _actionCts = null;
    }

    protected void OnSkillEnter()
    {
        if (skillTimeline == null)
        {
            _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Q_Skill);
            return;
        }

        IsUsingSkill = true;
        skillTimeline.Play();

        if (_gameCanvas == null)
        {
            var existingUI = FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
            if (existingUI != null)
            {
                existingUI.gameObject.SetActive(false);
                _gameCanvas = existingUI.gameObject;

                Managers.UI.SetActiveWorldCanvas(false);
            }
        }
        else
        {
            _gameCanvas.SetActive(false);
            Managers.UI.SetActiveWorldCanvas(false);
        }
    }

    protected virtual void OnCutsceneEnded(PlayableDirector director)
    {
        if (_gameCanvas != null)
        {
            _gameCanvas.SetActive(true);
            Managers.UI.SetActiveWorldCanvas(true);
        }
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Q_Skill);
    }

    // Stat(Health).OnDead 구독자. 사망 시 캐릭터 고유 반응 수행. (구 CharacterStat.HandleDeath의 collider 비활성화 흡수)
    private void HandleDeath()
    {
        // Collider 비활성화 (추가 피격 방지)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Death);
        OnCharacterDead?.Invoke(this);
    }

    #endregion

    #region Animation Events

    void ChangeToIdle()
    {
        if (_stateMachine.IsDead) return;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
        IsUsingSkill = false;
    }

    void CheckAttackFinished()
    {
        //if (_stateMachine.IsDead) return;

        //// Attack 상태가 아니면 이 이벤트가 발화될 이유 없음
        //if (_stateMachine.CurrentState != CharacterStateMachine.PlayerState.Attack)
        //{
        //    IsUsingSkill = false;
        //    return;
        //}

        //bool isMouseHeld = UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
        //bool shouldContinue = isMouseHeld && !Managers.UI.IsPopupOpen;

        //if (shouldContinue)
        //{
        //    anim.Play("Attack_Ing", -1, 0f); // 연사 유지
        //    return;
        //}

        //_stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
        //IsUsingSkill = false;

        if (_stateMachine.IsDead) return;

        // 현재 공격 상태가 아니면 무시 (스킬 등으로 캔슬된 경우)
        if (_stateMachine.CurrentState != CharacterStateMachine.PlayerState.Attack)
        {
            return;
        }

        // 애니메이션이 끝나면 무조건 Idle로 돌려보냄.
        // 마우스를 누르고 있다면 PlayerController가 쿨타임을 확인하고 즉시 다시 Attack 상태로 만듭니다.
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Idle);
        IsUsingSkill = false;
    }

    public void OnAttackEvent(AudioClip sfx)
    {
        if (_stateMachine.CurrentState != CharacterStateMachine.PlayerState.Attack) return;
        PerformAttackAction();
        Managers.Sound.Play(sfx, Define.Sound.Effect);
    }

    public void OnPlaySoundEvent(AudioClip clip)
    {
        if (clip != null) Managers.Sound.Play(clip, Define.Sound.Effect);
    }

    public void PlayAudio() { }

    #endregion

    #region Virtual Methods

    // 기본 평타 = 0번 어빌리티 발동. 대부분의 캐릭터는 이 기본만으로 충분하다(서브클래스 불필요).
    // 차징·특수 발동이 필요한 캐릭터만 override 한다.
    protected virtual void PerformAttackAction() => TryUseAbility(0);


    protected DamageInfo CalculatedDamage()
    {
        // 1. 플레이어의 스탯 가져오기
        float baseDamage = Stat.Attack.Value;
        float critRate = Stat.CritRate.Value;
        float critDamage = Stat.CritDamage.Value;

        // 2. 치명타 계산
        bool isCrit = UnityEngine.Random.value < critRate;

        // 3. 최종 데미지 산출
        float finalDamage = isCrit ? baseDamage * critDamage : baseDamage;

        // 4. 정보 캡슐화 후 반환
        return new DamageInfo(finalDamage, gameObject, isCrit);
    }

    protected virtual void PlaySFXOnly() { }
    protected virtual void PlaySFX() { }
    protected virtual void OnESkillEvent(AudioClip sfx) { }

    #endregion

    #region NPC Trigger

    private void OnTriggerEnter(Collider other)
    {
        var interactable = other.GetComponent<IInteractable>();
        if (interactable == null) return;
        CurrentInteractable = interactable;
        interactable.OnTargetEnter(this);
    }

    private void OnTriggerExit(Collider other)
    {
        var interactable = other.GetComponent<IInteractable>();
        if (interactable == null || CurrentInteractable != interactable) return;
        interactable.OnTargetExit(this);
        CurrentInteractable = null;
    }

    #endregion

    #region VFX

    public virtual async UniTaskVoid PlayHealingAura()
    {
        if (_healingAuraInstance == null)
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>("Healing_Aura", isGlobal: true);
            if (prefab == null) return;

            _healingAuraInstance = Managers.Resource.Instantiate(prefab, transform);
            _healingAuraInstance.transform.localPosition = Vector3.zero;
            _healingAuraInstance.transform.localRotation = Quaternion.identity;
            _healingAuraParticle = _healingAuraInstance.GetComponentInChildren<ParticleSystem>();
        }

        _healingAuraInstance?.SetActive(true);
        if (_healingAuraParticle != null)
        {
            _healingAuraParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _healingAuraParticle.Play(true);
        }

        Stat.HealthComp.Heal();
    }

    public virtual void StopHealingAura()
    {
        if (_healingAuraInstance == null || !_healingAuraInstance.activeSelf) return;
        _healingAuraParticle?.Stop();
        _healingAuraInstance.SetActive(false);
    }

    #endregion
}