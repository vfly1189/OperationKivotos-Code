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
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        // 쿨타임 체크
        if (Time.time - _lastSwapTime < _swapCooldown) return;

        // 키 입력 (1~4)
        if (Input.GetKeyDown(KeyCode.Alpha1)) TrySwap(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TrySwap(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TrySwap(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) TrySwap(3);
    }

    public void TrySwap(int targetIndex)
    {
        // 파티원 수보다 큰 번호거나, 이미 나와있는 캐릭터면 무시
        if (targetIndex >= PartyMembers.Count || targetIndex == _currentIndex) return;

        //// 죽은 캐릭터는 교체 불가 체크 (HP <= 0)
        //if (PartyMembers[targetIndex].IsDead)
        //{
        //    Debug.Log("캐릭터가 행동 불능 상태입니다.");
        //    return;
        //}

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
