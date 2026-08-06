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
    [SerializeField] protected Animator anim;

    [Header("Skill Settings")]
    [SerializeField] protected PlayableDirector skillTimeline;

    [Header("Combat Settings")]
    //[SerializeField] protected GameObject _bulletPrefab;
    [SerializeField] protected Transform _firePoint;
    //[SerializeField] protected ParticleSystem fireEffectParticle;

    [SerializeField] protected List<AbilitySlotEntry> _abilities = new List<AbilitySlotEntry>();
    // 슬롯 하나당 어빌리티 "리스트". 인스펙터에 같은 슬롯을 여러 개 넣으면 그 순서가 비트 인덱스가 된다.
    private Dictionary<CharacterAbilitySlot, List<AbilityData>> _slotMap;
    private readonly AbilityRunner _abilityRunner = new AbilityRunner();
    private GameplayTagContainer _ownedTags;   // 캐스터 보유 태그 (게이트 판독·GrantsTags 부여 대상)

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
    protected CharacterAnimationController _animController;
    protected GameObject _gameCanvas;

    // VFX
    protected static GameObject _healingAuraPrefab;
    protected GameObject _healingAuraInstance;
    protected ParticleSystem _healingAuraParticle;
    protected CancellationTokenSource _actionCts;

    public event Action<BaseCharacter> OnCharacterDead;

    #region Ability

    // 슬롯 → 어빌리티 리스트 매핑 구축. 역할(_eAbility) 해석 이후에 호출해야 한다.
    // 인스펙터 _abilities의 등장 순서 = 각 슬롯 리스트 내 비트 인덱스.
    protected void BuildSlotMap()
    {
        _slotMap = new Dictionary<CharacterAbilitySlot, List<AbilityData>>();
        foreach (var e in _abilities)
        {
            if (e.Ability == null) continue;
            if (!_slotMap.TryGetValue(e.Slot, out var list))
                _slotMap[e.Slot] = list = new List<AbilityData>();
            list.Add(e.Ability);
        }

        // 역할이 부여한 E를 Ex 슬롯 [0]으로 흡수 (별도 경로 제거)
        if (_eAbility != null)
        {
            if (!_slotMap.TryGetValue(CharacterAbilitySlot.E_Skill, out var exList))
                _slotMap[CharacterAbilitySlot.E_Skill] = exList = new List<AbilityData>();
            exList.Insert(0, _eAbility);
        }
    }

    // 비트(fire) 전용 진입점. slot = 어느 스킬(상태가 결정), beat = 그 슬롯 안에서 몇 번째 발사.
    // 평타·Q는 애니메이션 이벤트(AbilityBeat)가 이걸 부른다. 쿨/코스트는 admission(UseSkillQ 등의 Commit)이 소유하므로
    // 여기선 이펙트만 발사한다(무게이트).
    public void UseAbility(CharacterAbilitySlot slot, int beat = 0)
    {
        var token = _actionCts?.Token ?? CancellationToken.None;
        if (_slotMap.TryGetValue(slot, out var list) && beat >= 0 && beat < list.Count)
            _abilityRunner.Fire(list[beat], BuildCtx(), token).Forget();
    }


    private float SlotCooldown(CharacterAbilitySlot slot) => slot switch
    {
        CharacterAbilitySlot.Q_Skill => Stat.GetData().QSkillCoolTime,
        CharacterAbilitySlot.E_Skill => Stat.GetData().ESkillCoolTime,
        _ => 0f,   // 평타: 쿨 없음
    };

    public float SlotCooldownRemaining(CharacterAbilitySlot slot) => _slotMap.TryGetValue(slot, out var ability)
        && ability.Count > 0 ? _abilityRunner.CooldownRemaining(ability[0]) : 0f;

    public float SlotCooldownDuration(CharacterAbilitySlot slot) => _slotMap.TryGetValue(slot, out var ability)
        && ability.Count > 0 ? _abilityRunner.CooldownDuration(ability[0]) : 0f;

    // 궁극기(Q) 준비 = 에너지 만충 && Q 쿨 0. 쿨은 러너(절대시각)라 "쿨 끝남" 이벤트가 없어 UI가 폴링으로 읽는다.
    public bool IsUltimateReady =>
        Stat != null
        && Stat.CurrentEnergy >= Stat.MaxEnergy.Value
        && SlotCooldownRemaining(CharacterAbilitySlot.Q_Skill) <= 0f;

    private AbilityContext BuildCtx()
    {
        var ctx = new AbilityContext
        {
            Caster = this,
            CasterGO = gameObject,
            CasterStat = Stat,
            Object = _firePoint,   // 총구 = 스폰 기준 (aim은 캐릭터 회전이 firePoint에 반영됨)
            Target = null,
            TargetPoint = transform.position + transform.forward,
            Tags = _ownedTags,   // 태그 게이트 판독 대상
        };
        return ctx;
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

        // 보유 태그 컨테이너 확보 (게이트 판독용). 저장=Owner, 만료=StatusRunner, 판독=AbilityRunner.
        _ownedTags = GameplayTagOwner.EnsureOn(gameObject).Owned;

        // E스킬 해석 (모델 1): 캐릭터 오버라이드가 있으면 그것, 없으면 역할 기본 E
        _eAbility = _eAbilityOverride != null ? _eAbilityOverride
                  : (_roleData != null ? _roleData.eAbility : null);

        BuildSlotMap();

        _stateMachine = new CharacterStateMachine();
        _movement = new CharacterMovement(transform, _speed);
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

    public void BaseAttack(bool isPressing)
    {
        if (!isPressing) return;

        // 평타는 별도 쿨 없이 상태로 게이팅한다 — Attack 상태 진입 후 애니가 끝나야(→Idle) 재진입 가능.
        // 발사 간격 = 평타 애니 길이. (쿨을 원하면 평타 AbilityData.Cooldown으로 부여)
        if (!_stateMachine.CanAttack || IsUsingSkill || Stat.HealthComp.IsDead) return;

        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.Attack);
    }

    public void UseSkillQ()
    {
        if (!_stateMachine.CanUseSkill || IsUsingSkill || Stat.HealthComp.IsDead) return;
        if (!_slotMap.TryGetValue(CharacterAbilitySlot.Q_Skill, out var list) || list.Count == 0) return;

        AbilityData q0 = list[0];
        if (Stat.CurrentEnergy < Stat.MaxEnergy.Value) return;   // 궁게이지 만충이어야 발동

        // 쿨·궁게이지는 '입력 시점'에 소비한다(컷신 내내 쿨이 도는 게 보여야 함).
        // 이펙트는 이후 Q_Skill 애니의 AbilityBeat → UseAbility(Q_Skill) → Fire 로 나간다.
        var ctx = BuildCtx();
        ctx.CoolDown = SlotCooldown(CharacterAbilitySlot.Q_Skill);
        if (!_abilityRunner.Commit(q0, ctx)) return;             // 쿨 검사+시작(입력 시점)
        Stat.ConsumeUltimateGauge();                             // 궁게이지 0 + OnEnergyChanged

        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.QSkillCutScene);
    }

    public void UseSkillE()
    {
        if (!_stateMachine.CanUseSkill || IsUsingSkill || Stat.HealthComp.IsDead) return;

        if (!_slotMap.TryGetValue(CharacterAbilitySlot.E_Skill, out var list) || list.Count == 0) return;

        AbilityData e0 = list[0];

        // E는 즉발: 게이트(쿨)와 발사가 같은 순간. Commit(쿨 검사+시작) 통과 시에만 상태전이 후 Fire.
        var ctx = BuildCtx();
        ctx.CoolDown = SlotCooldown(CharacterAbilitySlot.E_Skill);
        if (!_abilityRunner.Commit(e0, ctx)) return;

        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.E_Skill);

        // ChangeState 후 새로 만들어진 액션 토큰으로 발사(상태가 다시 바뀌면 캐스트 취소되도록)
        var token = _actionCts?.Token ?? CancellationToken.None;
        _abilityRunner.Fire(e0, ctx, token).Forget();
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

    public void OnPlaySoundEvent(AudioClip clip)
    {
        if (clip != null) Managers.Sound.Play(clip, Define.Sound.Effect);
    }

    // 좌클릭·Q 공용 애니메이션 이벤트 콜백. 클립마다 콜백을 따로 만들 필요 없다.
    //  - 슬롯(어느 스킬)은 '현재 상태'가 결정한다.
    //  - intParameter = 그 슬롯 리스트 안에서 '몇 번째 발사(비트 인덱스)'.
    //  - objectReferenceParameter = (선택) 이 비트에서 재생할 SFX.
    public void AbilityBeat(AnimationEvent e)
    {
        if (_stateMachine.IsDead) return;

        // 상태 → 슬롯. 매칭 상태가 아니면(캔슬된 애니 잔여 비트 등) 무시.
        var slot = SlotForState(_stateMachine.CurrentState);
        if ((int)slot < 0) return;

        if (e.objectReferenceParameter is AudioClip sfx)
            Managers.Sound.Play(sfx, Define.Sound.Effect);

        UseAbility(slot, e.intParameter);   // intParameter = 비트 인덱스
    }

    // 현재 상태에 대응하는 슬롯. 대응이 없으면 -1(무효)을 돌려 비트를 무시하게 한다.
    private static CharacterAbilitySlot SlotForState(CharacterStateMachine.PlayerState s) => s switch
    {
        CharacterStateMachine.PlayerState.Attack  => CharacterAbilitySlot.BaseAttack,
        CharacterStateMachine.PlayerState.Q_Skill => CharacterAbilitySlot.Q_Skill,
        _ => (CharacterAbilitySlot)(-1),
    };

    public void PlayAudio() { }

    #endregion

    #region Virtual Methods

    // 기본 평타 = Attack 슬롯 발동. 대부분의 캐릭터는 이 기본만으로 충분하다(서브클래스 불필요).
    // 차징·특수 발동이 필요한 캐릭터만 override 한다.
    protected virtual void PerformAttackAction() => UseAbility(CharacterAbilitySlot.BaseAttack);

    protected virtual void PlaySFXOnly() { }
    protected virtual void PlaySFX() { }

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