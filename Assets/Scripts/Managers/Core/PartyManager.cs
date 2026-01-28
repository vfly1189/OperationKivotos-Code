using System;
using System.Collections.Generic;
using UnityEngine;

public class PartyManager : MonoBehaviour
{
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

    // 카메라 추적 타겟 관리용
    //public CameraController MainCamera;

    public PlayerController _playerController;
    //컨트롤러
    public PlayerController PlayerController { get; set; }

    public void Init(List<BaseCharacter> loadedCharacters)
    {
        PartyMembers = loadedCharacters;

        // 첫 번째 캐릭터만 활성화, 나머지는 비활성화
        for (int i = 0; i < PartyMembers.Count; i++)
        {
            if (i == 0) ActivateCharacter(i, Vector3.zero, Quaternion.identity); // 초기 위치
            else DeactivateCharacter(i);
        }

        Camera.main.GetComponent<CameraController>()._player = loadedCharacters[0].gameObject;
        if(_playerController != null)
        {
            //Debug.Log("PlayerController 있음");
            _playerController.SetControlTarget(loadedCharacters[0]);
        }
        else
        {
            //Debug.Log("PlayerController 없음");
        }

        _currentIndex = 0;

        RegisterInput();
    }

    void Update()
    {
        
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
        // 1. 쿨타임 체크 (HandleInput에서 가져옴)
        if (Time.time - _lastSwapTime < _swapCooldown) return;

        // 2. 인덱스, 중복 체크
        if (targetIndex >= PartyMembers.Count || targetIndex == _currentIndex) return;

        // 3. 스킬 사용 중 체크
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

        // ★ 핵심: 위치/회전 동기화
        // 나가려는 캐릭터의 현재 위치와 방향을 들어올 캐릭터에게 복사
        Vector3 position = prevChar.transform.position;
        Quaternion rotation = prevChar.transform.rotation;

        // 1. 이전 캐릭터 퇴장 (이펙트 처리 가능)
        DeactivateCharacter(prevIdx);

        // 2. 다음 캐릭터 등장
        ActivateCharacter(nextIdx, position, rotation);

        // 3. 카메라 타겟 변경
        if (Camera.main != null)
        {
            Camera.main.GetComponent<CameraController>()._player = nextChar.gameObject;
        }
        _playerController.SetControlTarget(PartyMembers[nextIdx]);

        // 4. 인덱스 갱신 및 쿨타임 적용
        _currentIndex = nextIdx;
        _lastSwapTime = Time.time;

        // 5. 교체 이펙트/사운드 재생 (Managers.Sound.Play...)
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
}
