using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }
    [Header("Party Settings")]
    // 실제 인게임에 로드된 캐릭터 인스턴스들 (최대 4명)
    public List<BaseCharacter> PartyMembers = new List<BaseCharacter>();

    // 현재 조작 중인 캐릭터의 인덱스
    private int _currentIndex = 0;

    // 교체 쿨타임 (원신은 약 1초)
    private float _swapCooldown = 1.0f;
    private float _lastSwapTime = -99f;

    //이벤트 정의: 캐릭터가 교체될 때 호출됨 (인자: 바뀐 캐릭터의 Index)
    public event Action<int> OnCharacterChanged;
    // [추가] 게임 종료 이벤트 (true: 성공, false: 실패)
    public event Action<bool> OnGameFinished;

    public PlayerController _playerController;
    //컨트롤러
    public PlayerController PlayerController { get; set; }

    private bool _isSwapping = false;


    #region 필수(Awake, Start, Init, Update ... )
    private void Awake()
    {
        // 2. 싱글톤 보장 로직
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 바뀌어도 파괴 금지

            // 씬 로드 이벤트 등록 (카메라 갱신 등을 위해)
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            // 이미 매니저가 있으면, 새로 생긴 나는 파괴 (GameScene 다시 왔을 때 등)
            Destroy(gameObject);
        }
    }
    private void OnDestroy()
    {
        // 파괴될 때 이벤트 구독 해제 (안 하면 메모리 누수/에러)
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
    public void Init(List<BaseCharacter> loadedCharacters)
    {
        _isSwapping = false;
        _currentIndex = 0;

        PartyMembers = loadedCharacters;

        // 첫 번째 캐릭터만 활성화, 나머지는 비활성화
        for (int i = 0; i < PartyMembers.Count; i++)
        {
            // 1. PartyManager도 이벤트를 듣습니다.
            // BaseCharacter가 듣는 것과는 별개입니다. (Multi-cast Delegate)
            BaseCharacter member = PartyMembers[i];

            member.Stat.OnDead += () => HandleCharacterDeath(member);

            if (i == 0) ActivateCharacter(i, Vector3.zero, Quaternion.identity); // 초기 위치
            else DeactivateCharacter(i);
        }

        Camera.main.GetComponent<CameraController>()._player = loadedCharacters[0].gameObject;
        if (_playerController != null)
        {
            //Debug.Log("PlayerController 있음");
            _playerController.SetControlTarget(loadedCharacters[0]);
        }


        _currentIndex = 0;

        RegisterInput();
    }
    void Update()
    {

    }
    #endregion


    // 씬이 로드될 때마다 호출됨
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 바뀌면 메인 카메라도 바뀌므로 갱신 필요
        if (Camera.main != null && PartyMembers.Count > 0)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null)
            {
                cam._player = GetCurrentCharacter().gameObject;
            }
        }
    }
    //씬 이동 시 파티원 전체 이동 편의 함수
    public void TeleportParty(Vector3 position)
    {
        foreach (var member in PartyMembers)
        {
            member.transform.position = position;
        }
        // 현재 활성 캐릭터 위치 강제 동기화
        GetCurrentCharacter().transform.position = position;
    }


    public void FinishGame(bool isSuccess)
    {
        //던전 클리어
        if(isSuccess)
        {
            OnGameFinished?.Invoke(isSuccess);
        }
        //던전 실패
        else
        {
            OnGameFinished?.Invoke(isSuccess);
            // 승리 시에도 씬 이동 시퀀스 실행 (패배 때와 비슷하게)
            StartCoroutine(CoGameOverSequence());
        }
    }


    //CharacterStat에서 OnDead가 발생하면 호출됨
    private void HandleCharacterDeath(BaseCharacter deadChar)
    {
        // 지금 죽은 게 현재 조작 중인 캐릭터가 맞는지 확인
        if (deadChar != GetCurrentCharacter()) return;

        // 이미 교체 시퀀스가 돌고 있다면 또 실행x
        if (_isSwapping) return;

        Debug.Log($"캐릭터 {deadChar.name} 사망! 자동 교체 시퀀스 시작.");

        // 코루틴으로 자동 교체 로직 위임
        StartCoroutine(CoAutoSwapAfterDeath());
    }

    //사망 후 자동 교체 코루틴
    private System.Collections.IEnumerator CoAutoSwapAfterDeath()
    {
        _isSwapping = true; // 교체 시작 잠금

        // (1) 조작 차단: 플레이어가 아무것도 못하게 막음
         _playerController.SetControlTarget(null); // 혹은 입력을 무시하는 상태로 전환

        //사망 애니메이션/연출 대기 
        yield return new WaitForSeconds(2.0f);

        //다음 살아있는 캐릭터 찾기
        int nextAliveIndex = FindNextAliveCharacterIndex();

        if (nextAliveIndex != -1)
        {
            // 살아있는 동료가 있다면 교체
            Debug.Log($"다음 생존자(인덱스 {nextAliveIndex})로 교체합니다.");
            SwapCharacter(_currentIndex, nextAliveIndex);
            _isSwapping = false; // 교체 완료 후 잠금 해제
        }
        else
        {
            Debug.Log("파티 전멸! 게임 오버.");
            //UI에게 "실패했다"고 알림
            //OnGameFinished?.Invoke(false); // false = Failed
            FinishGame(false);
            //yield return StartCoroutine(CoGameOverSequence());
        }
        
    }
    private IEnumerator CoGameOverSequence()
    {
        // UI 연출 시간(약 2~3초)만큼 대기
        yield return new WaitForSeconds(4.0f);

        // 사운드 정리 및 씬 재시작
        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game); // 또는 로비로 이동
    }

    // 살아있는 캐릭터 인덱스 찾기
    private int FindNextAliveCharacterIndex()
    {
        // 현재 인덱스 다음부터 한 바퀴 돌면서 찾기
        for (int i = 1; i < PartyMembers.Count; i++)
        {
            int checkIndex = (_currentIndex + i) % PartyMembers.Count;
            if (!PartyMembers[checkIndex].Stat.IsDead)
            {
                return checkIndex;
            }
        }
        return -1; // 다 죽음
    }

    

    void RegisterInput()
    {
        Managers.Input.RegisterAction("Swap_1", OnSwap1);
        Managers.Input.RegisterAction("Swap_2", OnSwap2);
        Managers.Input.RegisterAction("Swap_3", OnSwap3);
        Managers.Input.RegisterAction("Swap_4", OnSwap4);
    }

    void OnSwap1() => TrySwap(0);
    void OnSwap2() => TrySwap(1);
    void OnSwap3() => TrySwap(2);
    void OnSwap4() => TrySwap(3);

    public void TrySwap(int targetIndex)
    {        
        //쿨타임 체크 
        if (Time.time - _lastSwapTime < _swapCooldown) return;

        //인덱스, 중복 체크
        if (targetIndex >= PartyMembers.Count || targetIndex == _currentIndex) return;

        if (PartyMembers[targetIndex].Stat.IsDead)
        {
            Debug.Log("사망한 캐릭터로는 교체할 수 없습니다.");
            return;
        }


        //스킬 사용 중 체크
        BaseCharacter currentChar = PartyMembers[_currentIndex]; // targetIndex 아님! 현재 나와있는 애를 검사해야 함
        if (currentChar.IsUsingSkill)
        {
            Debug.Log("스킬 사용 중에는 교체할 수 없습니다!");
            return;
        }

        SwapCharacter(_currentIndex, targetIndex);
    }

    private void SwapCharacter(int prevIdx, int nextIdx)
    {
        BaseCharacter prevChar = PartyMembers[prevIdx];
        BaseCharacter nextChar = PartyMembers[nextIdx];

        // 위치/회전 동기화
        // 나가려는 캐릭터의 현재 위치와 방향을 들어올 캐릭터에게 복사
        Vector3 position = prevChar.transform.position;
        Quaternion rotation = prevChar.transform.rotation;

        //이전 캐릭터 퇴장 (이펙트 처리 가능)
        DeactivateCharacter(prevIdx);

        //다음 캐릭터 등장
        ActivateCharacter(nextIdx, position, rotation);

        //카메라 타겟 변경
        if (Camera.main != null)
        {
            Camera.main.GetComponent<CameraController>()._player = nextChar.gameObject;
        }
        _playerController.SetControlTarget(PartyMembers[nextIdx]);

        //인덱스 갱신 및 쿨타임 적용
        _currentIndex = nextIdx;
        _lastSwapTime = Time.time;

        //교체 이펙트/사운드 재생 (Managers.Sound.Play...)
        //Managers.Effect.Play("SwitchEffect", position);

        OnCharacterChanged?.Invoke(nextIdx);
    }

    private void ActivateCharacter(int index, Vector3 pos, Quaternion rot)
    {
        var character = PartyMembers[index];
        character.transform.position = pos;
        character.transform.rotation = rot;
        character.gameObject.SetActive(true);
        //character.OnSwitchIn(); // 등장 시 버프/대사 처리
    }

    private void DeactivateCharacter(int index)
    {
        var character = PartyMembers[index];
        //character.OnSwitchOut(); // 퇴장 시 버프 해제 등
        character.gameObject.SetActive(false);
    }

    public BaseCharacter GetCurrentCharacter()
    {
        return PartyMembers[_currentIndex];
    }

    public void ClearParty()
    {
        // 기존 멤버들을 물리적으로 파괴 (씬에 남아있지 않게)
        foreach (var member in PartyMembers)
        {
            if (member != null)
                Destroy(member.gameObject);
        }
        PartyMembers.Clear();

        // 각종 상태 변수 초기화
        _currentIndex = 0;
        _isSwapping = false;
        _lastSwapTime = -99f;
    }

    public void TurnOnAllMembers()
    {
        foreach (var member in PartyMembers)
        {
            member.gameObject.SetActive(true);
        }
    }
}
