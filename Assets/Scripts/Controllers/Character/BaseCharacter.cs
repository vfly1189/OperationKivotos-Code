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
    [SerializeField] protected GameObject _bulletPrefab;
    [SerializeField] protected Transform _firePoint;
    [SerializeField] protected ParticleSystem fireEffectParticle;

    [SerializeField] protected List<AbilityData> _abilities = new List<AbilityData>();
    private AbilityRunner _abilityRunner;

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

    public void TryUseAbility(int id)
    {

    }

    #endregion


    #region Lifecycle

    private void Awake() => Init();

    public virtual async void Init()
    {
        if (anim == null) anim = GetComponent<Animator>();
        Stat = GetComponent<CharacterStat>();
        Stat?.Init();

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
            Stat.OnDead -= HandleDeath;
            Stat.OnDead += HandleDeath;
        }
        _stateMachine?.ChangeState(CharacterStateMachine.PlayerState.Idle);
    }

    protected virtual void OnDisable()
    {
        if (Stat != null) Stat.OnDead -= HandleDeath;
        CancelCurrentAction();
    }

    #endregion

    #region API

    public void Move(Vector2 dir)
    {
        if (!_stateMachine.CanMove || Stat.IsDead) return;

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
        if (!_stateMachine.CanAttack || IsUsingSkill || Stat.IsDead) return;
        if (!_combat.CanAttack) return;

        if (_combat.TryAttack())
            _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Attack);
    }

    public void UseSkillQ()
    {
        if (!_stateMachine.CanUseSkill || IsUsingSkill || Stat.IsDead) return;
        if (!_combat.TryUseSkillQ()) return;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.QSkillCutScene);
    }

    public void UseSkillE()
    {
        if (!_stateMachine.CanUseSkill || IsUsingSkill || Stat.IsDead) return;
        if (!_combat.TryUseSkillE()) return;
        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.E_Skill);
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

        Stat.IsInvincible = newState == CharacterStateMachine.PlayerState.QSkillCutScene
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

    private void HandleDeath()
    {
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

    protected virtual void PerformAttackAction() { }

    protected virtual void PlayFireEffect()
    {
        if (fireEffectParticle == null) return;
        fireEffectParticle.Stop(true);
        fireEffectParticle.Play();
    }

    protected virtual void FireOneBullet()
    {
        GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);
        bulletObj.transform.rotation = transform.rotation;

        BulletController bulletScript = bulletObj.GetComponent<BulletController>();
        if (bulletScript != null && Stat != null)
            bulletScript.Init(CalculatedDamage(), gameObject);

        PlayFireEffect();
    }

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

        Stat.Heal();
    }

    public virtual void StopHealingAura()
    {
        if (_healingAuraInstance == null || !_healingAuraInstance.activeSelf) return;
        _healingAuraParticle?.Stop();
        _healingAuraInstance.SetActive(false);
    }

    #endregion
}