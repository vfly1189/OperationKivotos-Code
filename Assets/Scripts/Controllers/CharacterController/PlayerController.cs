using UnityEngine;

public class PlayerController
{
    // 현재 조작해야 할 대상(PartyManager가 꽂아줌)
    private BaseCharacter _currentTarget;

    // 마우스 상태 저장 (누르고 있는지 여부)
    private bool _isMousePressed = false;
    private Vector2 _currentMoveInput;

    public bool VictoryTime { get; set; }

    // 생성자에서 입력 이벤트 등록
    public PlayerController()
    {
        RegisterInputEvents();
    }

    // Start() 대신 생성자에서 호출
    private void RegisterInputEvents()
    {
        // 기존 구독이 있다면 먼저 해제 (중복 방지)
        Managers.Input.OnMoveInput -= HandleMove;
        Managers.Input.OnMoveInput += HandleMove;

        Managers.Input.MouseAction -= HandleMouse;
        Managers.Input.MouseAction += HandleMouse;

        Managers.Input.RegisterAction("Q_Skill", HandleSkill_Q);
        Managers.Input.RegisterAction("E_Skill", HandleSkill_E);
        Managers.Input.RegisterAction("Interact", HandleInteract);
    }

    // PartyManager가 호출해줄 함수
    public void SetControlTarget(BaseCharacter newTarget)
    {
        _currentTarget = newTarget;

        // 타겟이 바뀌면 마우스 누름 상태 초기화
        _isMousePressed = false;

        // 타겟 설정 시 현재 입력값이 있으면 즉시 적용
        if (_currentTarget != null && _currentMoveInput.sqrMagnitude > 0.01f)
        {
            _currentTarget.Move(_currentMoveInput);
        }
    }

    // 매 프레임 이동 입력 처리
    private void HandleMove(Vector2 dir)
    {
        _currentMoveInput = dir;
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
    private void HandleMouse(Define.MouseEvent evt)
    {
        if (_currentTarget == null) return;

        //// 마을(GameScene)에서는 공격 금지
        //if (Managers.SceneEx.CurrentSceneType == Define.Scene.Game)
        //{
        //    _isMousePressed = false;
        //    return;
        //}

        if (evt == Define.MouseEvent.Press)
        {
            _isMousePressed = true;
        }
        else if (evt == Define.MouseEvent.Click)
        {
            _isMousePressed = false;
        }
    }

    // 스킬 입력 처리
    private void HandleSkill_Q()
    {
        _currentTarget?.UseSkill_Q();
    }

    private void HandleSkill_E()
    {
        _currentTarget?.UseSkill_E();
    }

    private void HandleInteract()
    {
        if (_currentTarget == null || _currentTarget.Stat.IsDead) return;

        // 현재 컨트롤 중인 캐릭터 주변에 상호작용 가능한 NPC가 있다면
        if (_currentTarget.CurrentInteractable != null)
        {
            // 상호작용 실행! (NoahController.Interact 호출됨)
            _currentTarget.CurrentInteractable.Interact();
        }
    }

    // Update 로직 -> Managers.Update에서 호출
    public void OnUpdate()
    {
        if (_currentTarget == null || _currentTarget.Stat.IsDead) return;

        _currentTarget.Attack(_isMousePressed);
    }

    // 정리 메서드 (Dispose 시 호출)
    public void Dispose()
    {
        Managers.Input.OnMoveInput -= HandleMove;
        Managers.Input.MouseAction -= HandleMouse;
    }
}
