using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // 현재 조작해야 할 대상(PartyManager가 꽂아줌)
    private BaseCharacter _currentTarget;

    // 마우스 상태 저장 (누르고 있는지 여부)
    private bool _isMousePressed = false;

    void Start()
    {
        // 1. 입력 이벤트 등록 (한 번만 하면 됨)
        Managers.Input.OnMoveInput -= HandleMove;
        Managers.Input.OnMoveInput += HandleMove;

        Managers.Input.MouseAction -= HandleMouse;
        Managers.Input.MouseAction += HandleMouse;

        Managers.Input.RegisterAction("Q_Skill", HandleSkill_Q);
        Managers.Input.RegisterAction("E_Skill", HandleSkill_E);
    }

    // PartyManager가 호출해줄 함수
    public void SetControlTarget(BaseCharacter newTarget)
    {
        _currentTarget = newTarget;

        // 타겟이 바뀌면 마우스 누름 상태 같은 건 초기화해주는 게 안전함
        _isMousePressed = false;

        // 카메라 타겟 변경 (만약 CameraController가 있다면)
        // Camera.main.GetComponent<CameraController>().SetTarget(newTarget.transform);
    }

    // 매 프레임 이동 입력 처리
    void HandleMove(Vector2 dir)
    {
        if (_currentTarget == null) return;

        // 입력값이 있으면 이동 명령, 없으면 정지 명령
        if (dir.sqrMagnitude > 0.01f)
        {
            _currentTarget.Move(dir);
        }
        else
        {
            _currentTarget.StopMove();
        }
    }

    // 마우스 입력 처리 (공격)
    void HandleMouse(Define.MouseEvent evt)
    {
        if (_currentTarget == null) return;

        if (evt == Define.MouseEvent.Press)
        {
            _isMousePressed = true;
        }
        else if (evt == Define.MouseEvent.Click) // Click = Press 후 Release 된 시점 or 단순 뗌
        {
            _isMousePressed = false;
        }
    }

    // 스킬 입력 처리
    void HandleSkill_Q()
    {
        if (_currentTarget == null) return;

        _currentTarget.UseSkill_Q();
    }

    void HandleSkill_E()
    {
        if (_currentTarget == null) return;

        _currentTarget.UseSkill_E();
    }

    // Update에서는 지속적인 입력(공격 키 누르고 있기)을 처리
    void Update()
    {
        if (_currentTarget == null) return;

        // 마우스 누르고 있으면 계속 공격 명령 (연사)
        // 만약 단발 공격만 원한다면 HandleMouse에서 한 번만 호출하면 됨
        if (_isMousePressed)
        {
            _currentTarget.Attack(true);
        }
        else
        {
            // 공격 중지 신호 (손 뗐음)
            _currentTarget.Attack(false);
        }
    }
}
