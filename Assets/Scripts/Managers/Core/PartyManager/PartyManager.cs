using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PartyManager
{
    private readonly PartyRegistry _registry;
    private readonly PartySwapController _swapController;
    private readonly PartyCharacterActivator _activator;
    private readonly PartyDeathHandler _deathHandler;
    private readonly PartyProgress _progress;
    private PartyInputHandler _inputHandler;

    public event Action<GameObject> OnActiveCharacterChanged;
    public event Action<bool> OnGameFinished;
    public event Action<int> OnPartyLevelChanged;
    public event Action<float, float> OnPartyExpChanged;
    public event Action OnPartyWiped;

    public PlayerController PlayerController { get; private set; }

    private readonly GameObject _characterContainer;
    private bool _isGameEnding = false;

    public PartyManager(Transform managersTransform)
    {
        PlayerController = new PlayerController();

        _registry = new PartyRegistry();
        _swapController = new PartySwapController(_registry);
        _activator = new PartyCharacterActivator(_registry);
        _deathHandler = new PartyDeathHandler(_registry, _swapController);
        _progress = new PartyProgress();

        _swapController.OnSwapRequested += HandleSwapRequest;
        _deathHandler.OnPartyWiped += HandlePartyWiped;
        _progress.OnLevelChanged += HandlePartyLevelChanged;
        _progress.OnExpChanged += HandlePartyExpChanged;

        SceneManager.sceneLoaded += OnSceneLoaded;

        _characterContainer = new GameObject("@PartyMembers");
        _characterContainer.transform.SetParent(managersTransform);
    }

    // ── Swap ──────────────────────────────────────────────────────

    private void HandleSwapRequest(int prevIdx, int nextIdx)
    {
        _activator.SyncSwap(prevIdx, nextIdx);

        // 마우스 홀드 상태 초기화 후 새 타겟 연결
        PlayerController?.ClearMouseState();
        PlayerController?.SetControlTarget(_registry.GetCurrent());

        OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);
    }

    // ── Party Wipe / Game Over ────────────────────────────────────

    public void CancelDeathTasks()
    {
        _deathHandler.CancelDeathTasks();
    }

    private void HandlePartyWiped()
    {
        OnPartyWiped?.Invoke();
        //FinishGame(false);
    }

    public void FinishGame(bool isSuccess)
    {
        if (_isGameEnding) return;
        _isGameEnding = true;

        OnGameFinished?.Invoke(isSuccess);

        if (!isSuccess)
            GameOverSequenceAsync().Forget();
    }

    private async UniTaskVoid GameOverSequenceAsync()
    {
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(5.0f))
                                       .SuppressCancellationThrow();
        if (isCanceled) return;

        _isGameEnding = false;
        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game);
    }

    // ── Init / Clear ──────────────────────────────────────────────

    public void Init(List<BaseCharacter> characters, Transform spawnPoint)
    {
        ClearParty();
        _registry.Init(characters);

        foreach (var member in characters)
        {
            if (member?.Stat == null) continue;
            member.OnCharacterDead -= HandleMemberDeath;
            member.OnCharacterDead += HandleMemberDeath;
        }

        _activator.ActivateCharacter(0, spawnPoint.position, Quaternion.identity);
        for (int i = 1; i < characters.Count; i++)
            _activator.DeactivateCharacter(i);

        _inputHandler = new PartyInputHandler(_swapController);
        PlayerController?.SetControlTarget(_registry.GetCurrent());
    }

    public void ClearParty()
    {
        if (_registry.Members == null) return;

        foreach (var member in _registry.Members)
        {
            if (member == null) continue;
            member.OnCharacterDead -= HandleMemberDeath;
            UnityEngine.Object.Destroy(member.gameObject);
        }

        _registry.Members.Clear();
        _deathHandler.CancelDeathTasks();
    }

    // ── Scene Reset (필드 부활) ───────────────────────────────────

    

    public void ResetPartyForNewScene(Transform spawnPoint)
    {
        if (_registry.Members == null || _registry.Members.Count == 0) return;

        // 1. 모든 멤버 상태 싹 초기화 및 비활성화 (위치 이동은 아직 안 함)
        for (int i = 0; i < _registry.Members.Count; i++)
        {
            var member = _registry.Members[i];
            if (member == null) continue;

            // ResetCharacterState의 파라미터로 현재 위치를 그냥 줘버림 (상태만 리셋)
            member.ResetCharacterState(member.transform.position, member.transform.rotation);
            member.gameObject.SetActive(false);
        }

        // 2. 강제로 0번(리더) 캐릭터로 스왑 처리 (위치가 어디든 상관없이 0번을 활성 타겟으로 잡음)
        _swapController.TrySwap(0, isForce: true);

        // 3. 리더만 활성화 (이때 아직 이전 위치임)
        _registry.Members[0].gameObject.SetActive(true);

        // 4. [핵심] CC 충돌 없는 안전한 텔레포트 함수로 전원 스폰 지점으로 텔레포트!
        TeleportParty(spawnPoint.position);

        // 방향(Rotation)도 맞춰주기
        foreach (var member in _registry.Members)
        {
            member.transform.rotation = spawnPoint.rotation;
        }

        // 5. 컨트롤러 타겟 연결 및 이벤트 방송
        PlayerController?.ClearMouseState();
        PlayerController?.SetControlTarget(_registry.GetCurrent());
        OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);

        _isGameEnding = false;
    }

    public void ReturnToTownForNewScene(Transform spawnPoint)
    {
        if (_registry.Members == null || _registry.Members.Count == 0) return;

        for (int i = 0; i < _registry.Members.Count; i++)
        {
            var member = _registry.Members[i];
            if (member == null) continue;

            // ResetCharacterState 대신 ReturnToTownCharacterState 호출
            member.ReturnToTownCharacterState(member.transform.position, member.transform.rotation);
            member.gameObject.SetActive(false);
        }

        // 0번(리더) 캐릭터로 강제 스왑
        _swapController.TrySwap(0, isForce: true);
        _registry.Members[0].gameObject.SetActive(true);

        TeleportParty(spawnPoint.position);
        foreach (var member in _registry.Members) member.transform.rotation = spawnPoint.rotation;

        PlayerController?.ClearMouseState();
        PlayerController?.SetControlTarget(_registry.GetCurrent());
        OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);

        _isGameEnding = false;
    }

    // ── Death ─────────────────────────────────────────────────────

    private void HandleMemberDeath(BaseCharacter deadCharacter)
    {
        if (deadCharacter != null)
            _deathHandler.HandleCharacterDeathAsync(deadCharacter).Forget();
    }

    // ── Exp / Level ───────────────────────────────────────────────

    public void AddExp(float amount) => _progress.AddExp(amount);

    private void HandlePartyLevelChanged(int level)
    {
        OnPartyLevelChanged?.Invoke(level);
        BroadcastLevelToMembers();
    }

    private void HandlePartyExpChanged(float cur, float max) =>
        OnPartyExpChanged?.Invoke(cur, max);

    private void BroadcastLevelToMembers()
    {
        foreach (var member in GetMember())
            member.Stat.UpdateBaseStatsByPartyLevel(_progress.Level);
    }

    public void InitFromContext(PartyRuntimeData data)
    {
        _progress.InitFromData(data?.partyLevel ?? 1, data?.partyCurrentExp ?? 0f);
        BroadcastLevelToMembers();
    }

    public void SetPartyInvincible(bool invincible)
    {
        for (int i = 0; i < _registry.Members.Count; i++)
        {
            var member = _registry.Members[i];
            if (member == null) continue;

            member.Stat.IsInvincible = invincible;
        }
    }

    // ── Scene Load ────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_registry?.Members?.Count > 0)
            OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);
    }

    // ── Dispose ───────────────────────────────────────────────────

    public void Dispose()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ClearParty();
        PlayerController?.Dispose();
        _progress.OnLevelChanged -= HandlePartyLevelChanged;
        _progress.OnExpChanged -= HandlePartyExpChanged;
    }

    // ── Getters ───────────────────────────────────────────────────

    public int GetCurrentCharacterIndex() => _registry.CurrentIndex;
    public BaseCharacter GetCurrentCharacter() => _registry.GetCurrent();
    public List<BaseCharacter> GetMember() => _registry.Members;
    public Transform GetCharacterContainer() => _characterContainer.transform;

    public void TrySwap(int targetIndex) => _swapController.TrySwap(targetIndex);
    public void TeleportParty(Vector3 pos) => _activator.TeleportAll(pos);

    public int PartyLevel => _progress.Level;
    public float PartyCurrentExp => _progress.CurrentExp;
    public float PartyRequiredExp => _progress.RequiredExp;
}