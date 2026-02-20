using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PartyManager
{
    private Dictionary<BaseCharacter, Action> _deathHandlers = new Dictionary<BaseCharacter, Action>();

    // 하위 시스템들
    private PartyRegistry _registry;
    private PartySwapController _swapController;
    private PartyCharacterActivator _activator;
    private PartyDeathHandler _deathHandler;
    private PartyInputHandler _inputHandler;

    // 외부 노출 이벤트 (카메라/컨트롤러가 구독)
    public event Action<GameObject> OnActiveCharacterChanged;
    public event Action<bool> OnGameFinished;

    // PlayerController 참조 (외부 주입)
    public PlayerController PlayerController { get; private set; }

    // 코루틴 실행용 MonoBehaviour 참조
    private MonoBehaviour _coroutineRunner;

    // 캐릭터들의 부모 역할을 할 GameObject (한 번만 생성)
    private GameObject _characterContainer;

    private bool _isGameEnding = false;

    // 생성자로 Managers 받기
    public PartyManager(MonoBehaviour coroutineRunner)
    {
        _coroutineRunner = coroutineRunner;
        InitializeSubsystems();
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 캐릭터 컨테이너 생성
        CreateCharacterContainer();
    }

    private void CreateCharacterContainer()
    {
        // @PartyMembers 오브젝트 생성
        _characterContainer = new GameObject("@PartyMembers");
        _characterContainer.transform.SetParent(((MonoBehaviour)_coroutineRunner).transform); // Managers의 자식
    }   

    // 의존성 주입으로 하위 시스템 생성
    private void InitializeSubsystems()
    {
        PlayerController = new PlayerController();

        _registry = new PartyRegistry();
        _swapController = new PartySwapController(_registry);
        _activator = new PartyCharacterActivator(_registry);
        _deathHandler = new PartyDeathHandler(_registry, _swapController, _coroutineRunner);

        // 이벤트 체인 연결
        _swapController.OnSwapRequested += HandleSwapRequest;
        _deathHandler.OnPartyWiped += () => FinishGame(false);
    }

    // 교체 요청 처리 (카메라 + 컨트롤러 갱신)
    private void HandleSwapRequest(int prevIdx, int nextIdx)
    {
        _activator.SyncSwap(prevIdx, nextIdx);

        // 플레이어 컨트롤러 타겟 변경
        if (PlayerController != null)
        {
            PlayerController.SetControlTarget(_registry.GetCurrent());
        }

        // 외부에 알림
        OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);
    }

    // 파티 초기화
    public void Init(List<BaseCharacter> characters)
    {
        // 기존 멤버 이벤트 완전 정리
        if (_registry.Members != null && _registry.Members.Count > 0)
        {
            foreach (var oldMember in _registry.Members)
            {
                if (oldMember != null && oldMember.Stat != null && _deathHandlers.ContainsKey(oldMember))
                {
                    oldMember.Stat.OnDead -= _deathHandlers[oldMember];
                }
            }
            _deathHandlers.Clear();
        }

        _registry.Init(characters);

        // 사망 이벤트 구독 (메서드 참조 방식)
        foreach (var member in characters)
        {
            // 각 캐릭터마다 Action을 저장해서 나중에 정확히 해제 가능
            Action handler = () => _deathHandler.HandleCharacterDeath(member);
            _deathHandlers[member] = handler;
            member.Stat.OnDead += handler;
        }

        // 첫 번째 캐릭터만 활성화
        _activator.ActivateCharacter(0, Vector3.zero, Quaternion.identity);
        for (int i = 1; i < characters.Count; i++)
        {          
            _activator.DeactivateCharacter(i);
        }

        // 입력 핸들러 초기화
        _inputHandler = new PartyInputHandler(_swapController);

        // 컨트롤러 연동
        if (PlayerController != null)
        {
            PlayerController.SetControlTarget(_registry.GetCurrent());
        }
    }

    // 씬 로드 시 카메라 갱신
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_registry != null && _registry.Members != null && _registry.Members.Count > 0)
        {
            OnActiveCharacterChanged?.Invoke(_registry.GetCurrent().gameObject);
        }
    }

    private IEnumerator CoGameOverSequence()
    {
        yield return new WaitForSeconds(4.0f);

        _isGameEnding = false; //  플래그 리셋 (씬 전환 전)

        Debug.Log($"{_isGameEnding} false 전환");

        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game);
    }




    #region 외부 호출용 및 Getter
    public void ClearParty()
    {
        if (_registry.Members == null) return;

        // 저장된 핸들러로 정확히 이벤트 해제
        foreach (var member in _registry.Members)
        {
            if (member != null && member.Stat != null && _deathHandlers.ContainsKey(member))
            {
                member.Stat.OnDead -= _deathHandlers[member];
            }

            if (member != null)
            {
                UnityEngine.Object.Destroy(member.gameObject);
            }
        }

        _registry.Members.Clear();
        _deathHandlers.Clear(); // Dictionary도 초기화
    }

    public void TurnOnAllMembers()
    {
        foreach (var member in _registry.Members)
        {
            member.gameObject.SetActive(true);
        }
    }

    // Public API
    public int GetCurrentCharacterIndex() => _registry.CurrentIndex;
    public BaseCharacter GetCurrentCharacter() => _registry.GetCurrent();
    public void TrySwap(int targetIndex) => _swapController.TrySwap(targetIndex);
    public void TeleportParty(Vector3 pos) => _activator.TeleportAll(pos);
    public List<BaseCharacter> GetMemeber() => _registry.Members;
    // 파티 초기화 시 컨테이너 참조 전달
    public Transform GetCharacterContainer() => _characterContainer.transform;


    public void FinishGame(bool isSuccess)
    {
        Debug.Log("Finish Game 호출");

        //if (_isGameEnding) return; // [추가]
        //_isGameEnding = true;
        Debug.Log("_isGameEnding false 였음");
        OnGameFinished?.Invoke(isSuccess);


        if (!isSuccess)
        {
            _coroutineRunner.StartCoroutine(CoGameOverSequence());
        }
    }
    #endregion


    // 파괴 시 정리 (Managers에서 호출)
    public void Dispose()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // 파티 정리
        ClearParty();

        // PlayerController 정리
        if (PlayerController != null)
        {
            PlayerController.Dispose();
        }
    }
}
