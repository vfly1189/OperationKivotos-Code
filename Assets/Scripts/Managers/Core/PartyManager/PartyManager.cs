using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public class PartyManager
{
    private PartyRegistry _registry;
    private PartySwapController _swapController;
    private PartyCharacterActivator _activator;
    private PartyDeathHandler _deathHandler;
    private PartyInputHandler _inputHandler;
    private PartyProgress _progress;

    public event Action<GameObject> OnActiveCharacterChanged;
    public event Action<bool> OnGameFinished;
    public event Action<int> OnPartyLevelChanged;
    public event Action<float, float> OnPartyExpChanged;

    public PlayerController PlayerController { get; private set; }

    private GameObject _characterContainer;
    private bool _isGameEnding = false;

    public PartyManager(Transform managersTransform)
    {
        InitializeSubsystems();
        SceneManager.sceneLoaded += OnSceneLoaded;

        _characterContainer = new GameObject("@PartyMembers");
        _characterContainer.transform.SetParent(managersTransform);
    }

    private void InitializeSubsystems()
    {
        PlayerController = new PlayerController();

        _registry = new PartyRegistry();
        _swapController = new PartySwapController(_registry);
        _activator = new PartyCharacterActivator(_registry);
        _deathHandler = new PartyDeathHandler(_registry, _swapController);
        _progress = new PartyProgress();

        _swapController.OnSwapRequested += HandleSwapRequest;
        _deathHandler.OnPartyWiped += () => FinishGame(false);
        _progress.OnLevelChanged += HandlePartyLevelChanged;
        _progress.OnExpChanged += HandlePartyExpChanged;
    }

    private void HandleSwapRequest(int prevIdx, int nextIdx)
    {
        _activator.SyncSwap(prevIdx, nextIdx);

        if (PlayerController != null)
            PlayerController.SetControlTarget(_registry.GetCurrent());

        OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);
    }

    public void Init(List<BaseCharacter> characters, Transform spawnPoint)
    {
        ClearParty(); // 기존 멤버 정리

        _registry.Init(characters);

        // [핵심 2] 딕셔너리 없이 명시적인 함수 연결로 메모리 릭 원천 차단
        foreach (var member in characters)
        {
            if (member != null && member.Stat != null)
            {
                member.Stat.OnDead -= HandleMemberDeath;
                member.Stat.OnDead += HandleMemberDeath;
            }
        }

        _activator.ActivateCharacter(0, spawnPoint.position, Quaternion.identity);
        for (int i = 1; i < characters.Count; i++)
        {
            _activator.DeactivateCharacter(i);
        }

        _inputHandler = new PartyInputHandler(_swapController);

        if (PlayerController != null)
            PlayerController.SetControlTarget(_registry.GetCurrent());
    }

    // 사망 이벤트 중계용 래퍼 함수 (안전한 구독/해제를 위해 사용)
    private void HandleMemberDeath()
    {
        // 누가 죽었는지 찾아서 핸들러에 넘겨줌 (실무에서는 OnDead(BaseCharacter)로 매개변수를 넘기는 것을 강력 권장합니다!)
        BaseCharacter deadChar = null;
        foreach (var member in _registry.Members)
        {
            if (member.Stat.CurrentHp <= 0) deadChar = member;
        }

        if (deadChar != null)
            _deathHandler.HandleCharacterDeathAsync(deadChar).Forget(); // UniTaskVoid Fire-and-forget
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_registry != null && _registry.Members != null && _registry.Members.Count > 0)
        {
            OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);
        }
    }

    public void ClearParty()
    {
        if (_registry.Members == null) return;

        // [핵심 3] 명시적으로 이벤트 해제
        foreach (var member in _registry.Members)
        {
            if (member != null)
            {
                if (member.Stat != null)
                    member.Stat.OnDead -= HandleMemberDeath;

                UnityEngine.Object.Destroy(member.gameObject);
            }
        }
        _registry.Members.Clear();
        _deathHandler.CancelDeathTasks(); // 씬 전환 시 진행 중인 사망 루틴 즉시 취소
    }

    public void FinishGame(bool isSuccess)
    {
        if (_isGameEnding) return;
        _isGameEnding = true;

        OnGameFinished?.Invoke(isSuccess);

        if (!isSuccess)
        {
            GameOverSequenceAsync().Forget(); // 코루틴 대체
        }
    }
  
    private async UniTaskVoid GameOverSequenceAsync()
    {
        // SceneManagerEx나 시스템 단의 취소가 발생할 수 있으므로 안전망 적용
        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(4.0f)).SuppressCancellationThrow();
        if (isCanceled) return;

        _isGameEnding = false;

        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game);
    }

    public void ResetPartyForNewScene(Transform spawnPoint)
    {
        if (_registry.Members == null || _registry.Members.Count == 0) return;

        for (int i = 0; i < _registry.Members.Count; i++)
        {
            var member = _registry.Members[i];
            if (member == null) continue;

            member.ResetCharacterState(spawnPoint.position, spawnPoint.rotation);
            _activator.DeactivateCharacter(i);
        }

        OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);
        _activator.ActivateCharacter(0, spawnPoint.position, spawnPoint.rotation);

        _swapController.TrySwap(0);
        _isGameEnding = false;
    }

    // 경험치 레벨 관련
    public void AddExp(float amount)
    {
        _progress.AddExp(amount);
        // LevelUp이 발생하면 HandlePartyLevelChanged가 호출됨
    }
    private void HandlePartyLevelChanged(int level)
    {
        OnPartyLevelChanged?.Invoke(level);
        BroadcastLevelToMembers();
    }
    private void HandlePartyExpChanged(float cur, float max)
    {
        OnPartyExpChanged?.Invoke(cur, max);
    }
    private void BroadcastLevelToMembers()
    {
        foreach (var member in GetMemeber())
            member.Stat.UpdateBaseStatsByPartyLevel(_progress.Level);
    }

    public void InitFromContext(PartyRuntimeData data)
    {
        int level = data != null ? data.partyLevel : 1;
        float exp = data != null ? data.partyCurrentExp : 0f;

        _progress.InitFromData(level, exp);

        // 레벨에 맞춰 모든 멤버 스탯 갱신
        BroadcastLevelToMembers();
    }



    public void Dispose()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ClearParty();
        if (PlayerController != null) PlayerController.Dispose();

        _progress.OnLevelChanged -= HandlePartyLevelChanged;
        _progress.OnExpChanged -= HandlePartyExpChanged;
    }

    // Getter & Delegate
    public int GetCurrentCharacterIndex() => _registry.CurrentIndex;
    public BaseCharacter GetCurrentCharacter() => _registry.GetCurrent();
    public void TrySwap(int targetIndex) => _swapController.TrySwap(targetIndex);
    public void TeleportParty(Vector3 pos) => _activator.TeleportAll(pos);
    public List<BaseCharacter> GetMemeber() => _registry.Members;
    public Transform GetCharacterContainer() => _characterContainer.transform;
    public int PartyLevel => _progress.Level;
    public float PartyCurrentExp => _progress.CurrentExp;
    public float PartyRequiredExp => _progress.RequiredExp;
}
