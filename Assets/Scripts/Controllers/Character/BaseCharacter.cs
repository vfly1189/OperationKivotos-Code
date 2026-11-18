using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Playables;

public class BaseCharacter : MonoBehaviour, IAnchorSet
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

    [Header("Skill (SkillSystem) — 몬스터 · 보스와 같은 실행 구조, 고르는 곳만 입력")]
    [SerializeField] protected Pattern _attackPattern;     // 평타 — Attack 상태 동안 애니 비트를 받는다
    [SerializeField] protected Pattern _qPattern;          // Q — 컷신 뒤 Q_Skill 상태 동안 애니 비트를 받는다 (없으면 Q 불가)
    [Tooltip("애니 이벤트 AbilityBeat의 intParameter(비트 번호) → 이 목록의 태그로 SkillSystem에 보낸다")]
    [SerializeField] protected List<GameplayTagSO> _beatTags = new List<GameplayTagSO>();
    private SkillSystem _ss;

    [Header("Role")]
    [SerializeField] protected RoleDataSO _roleData;          // 역할 (= E스킬을 결정)
    [SerializeField] protected Pattern _ePatternOverride;     // 있으면 역할 기본 E를 덮어씀
    [Tooltip("역할 E가 없을 때 쓰는 이 캐릭터의 E — 비어 있으면 쿨 · 애니만")]
    [SerializeField] protected Pattern _eDefaultPattern;
    protected Pattern _ePattern;                              // 해석된 E (override ?? 역할 기본의 복제 ?? 기본)

    // 이관 원본 (AbilitySystem) — Tools/Player/Migrate Abilities to SkillSystem 이 읽는다. 이관 확인 뒤 삭제
    [SerializeField, HideInInspector] protected List<AbilitySlotEntry> _abilities = new List<AbilitySlotEntry>();
    [SerializeField, HideInInspector] protected AbilityData _eAbilityOverride;

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

    #region Skill

    private Pattern SlotPattern(CharacterAbilitySlot slot) => slot switch
    {
        CharacterAbilitySlot.BaseAttack => _attackPattern,
        CharacterAbilitySlot.Q_Skill => _qPattern,
        CharacterAbilitySlot.E_Skill => _ePattern,
        _ => null,
    };

    private float SlotCooldown(CharacterAbilitySlot slot) => slot switch
    {
        CharacterAbilitySlot.Q_Skill => Stat.GetData().QSkillCoolTime,
        CharacterAbilitySlot.E_Skill => Stat.GetData().ESkillCoolTime,
        _ => 0f,   // 평타: 쿨 없음
    };

    public float SlotCooldownRemaining(CharacterAbilitySlot slot) => _ss != null ? _ss.CooldownRemaining(SlotPattern(slot)) : 0f;

    public float SlotCooldownDuration(CharacterAbilitySlot slot) => _ss != null ? _ss.CooldownDuration(SlotPattern(slot)) : 0f;

    // 궁극기(Q) 준비 = 에너지 만충 && Q 쿨 0. 쿨은 SkillSystem(절대시각)이라 "쿨 끝남" 이벤트가 없어 UI가 폴링으로 읽는다.
    public bool IsUltimateReady =>
        Stat != null
        && Stat.CurrentEnergy >= Stat.MaxEnergy.Value
        && SlotCooldownRemaining(CharacterAbilitySlot.Q_Skill) <= 0f;

    // 몬스터의 BuildContext와 같은 자리 — 시전자 · 소켓(총구) · 스탯 · 지금 상태의 토큰
    private PatternContext BuildCtx() => new PatternContext
    {
        owner = _ss,
        targets = null,
        sockets = this,
        cToken = _actionCts != null ? _actionCts.Token : CancellationToken.None,
        stat = Stat,
    };

    // 소켓 — "Muzzle" = 총구, 그 밖은 VfxAnchorSet의 부착점
    public bool TryGet(string key, out Transform t)
    {
        t = null;
        if (key == "Muzzle") t = _firePoint;
        else if (TryGetComponent(out VfxAnchorSet set)) t = set.Get(key);
        return t != null;
    }

    // 역할 E — 역할이 든 패턴 프리팹을 내 밑에 하나 복제 (같은 역할끼리 프리팹 공유, 실행 상태는 캐릭터마다)
    private void ResolveEPattern()
    {
        if (_ePattern != null) return;
        if (_ePatternOverride != null) { _ePattern = _ePatternOverride; return; }
        if (_roleData == null || _roleData.ePattern == null) { _ePattern = _eDefaultPattern; return; }

        Transform parent = transform.Find("Skills");
        if (parent == null) parent = transform;
        _ePattern = Instantiate(_roleData.ePattern, parent);
        _ePattern.name = _roleData.ePattern.name;
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

        // 보유 태그 컨테이너 확보 (상태 태그 부착 대상). 저장=Owner, 만료=StatusRunner.
        GameplayTagOwner.EnsureOn(gameObject);

        // 스킬 실행 = SkillSystem (몬스터 · 보스와 같은 컴포넌트)
        _ss = GetComponent<SkillSystem>();
        if (_ss == null) _ss = gameObject.AddComponent<SkillSystem>();

        // E스킬 해석: 캐릭터 오버라이드가 있으면 그것, 없으면 역할 기본 E
        ResolveEPattern();

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
        _healingAuraPrefab = await Managers.Resource.LoadAsync<GameObject>("Healing_Aura", ResourceScopeType.Global);
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
        _ss?.Cancel();   // 교체로 꺼질 때 하던 패턴을 들고 있지 않게
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
        if (_qPattern == null) return;
        if (Stat.CurrentEnergy < Stat.MaxEnergy.Value) return;   // 궁게이지 만충이어야 발동

        // 쿨·궁게이지는 '입력 시점'에 소비한다(컷신 내내 쿨이 도는 게 보여야 함).
        // 패턴은 컷신 뒤 Q_Skill 상태에 들어갈 때 시작하고, 그 애니의 비트를 받는다 (OnStateChanged).
        if (!_ss.TryCommit(_qPattern, SlotCooldown(CharacterAbilitySlot.Q_Skill))) return;   // 쿨 검사+시작(입력 시점)
        Stat.ConsumeUltimateGauge();                             // 궁게이지 0 + OnEnergyChanged

        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.QSkillCutScene);
    }

    public void UseSkillE()
    {
        if (!_stateMachine.CanUseSkill || IsUsingSkill || Stat.HealthComp.IsDead) return;

        if (_ePattern == null) return;

        // E는 즉발: 게이트(쿨)와 시작이 같은 순간. 쿨 통과 시에만 상태전이 후 시작.
        if (!_ss.TryCommit(_ePattern, SlotCooldown(CharacterAbilitySlot.E_Skill))) return;

        _stateMachine.ChangeState(CharacterStateMachine.PlayerState.E_Skill);

        // ChangeState 후 새로 만들어진 액션 토큰으로 시작(상태가 다시 바뀌면 끊기도록)
        _ss.PlaySkill(_ePattern, BuildCtx(), beginNow: true);
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

        // 상태가 바뀌면 하던 패턴은 끝 — 새 상태가 패턴을 갖고 있으면 그걸 시작 (비트를 받는 쪽)
        if (_ss != null)
        {
            _ss.Cancel();
            Pattern p = newState switch
            {
                CharacterStateMachine.PlayerState.Attack => _attackPattern,
                CharacterStateMachine.PlayerState.Q_Skill => _qPattern,
                _ => null,
            };
            if (p != null) _ss.PlaySkill(p, BuildCtx(), beginNow: true);
        }

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

        // 지금 상태의 패턴(OnStateChanged에서 시작)이 이 비트 태그를 받아 Task → Event를 실행한다
        int beat = e.intParameter;
        if (_ss != null && beat >= 0 && beat < _beatTags.Count && _beatTags[beat] != null)
            _ss.SendTag(_beatTags[beat]);
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
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>("Healing_Aura", ResourceScopeType.Global);
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