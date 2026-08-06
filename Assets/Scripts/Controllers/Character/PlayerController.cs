using System.Threading.Tasks;
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

        // 액션 인텐트도 중복 구독 방지 (이동/마우스와 동일한 unregister→register 패턴).
        // 이 콜백들은 Dispose에서 대칭으로 해제한다 (씬 재진입 시 스택 방지).
        Managers.Input.UnregisterAction(InputIntent.Info, HandleInfo);
        Managers.Input.RegisterAction(InputIntent.Info, HandleInfo);

        Managers.Input.UnregisterAction(InputIntent.Inventory, HandleInventory);
        Managers.Input.RegisterAction(InputIntent.Inventory, HandleInventory);

        Managers.Input.UnregisterAction(InputIntent.SkillQ, HandleSkillQ);
        Managers.Input.RegisterAction(InputIntent.SkillQ, HandleSkillQ);

        Managers.Input.UnregisterAction(InputIntent.SkillE, HandleSkillE);
        Managers.Input.RegisterAction(InputIntent.SkillE, HandleSkillE);

        Managers.Input.UnregisterAction(InputIntent.Interact, HandleInteract);
        Managers.Input.RegisterAction(InputIntent.Interact, HandleInteract);
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
        // 팝업 열림 시 입력 차단은 InputManager의 컨텍스트 게이트가 담당한다.
        // (UI 진입 순간 게이트가 Click을 한 번 쏴 누름 상태를 자동 해제)
        if (_currentTarget == null) return;

        if (evt == Define.MouseEvent.Press)
        {
            _currentTarget.BaseAttack(true);
            //_isMousePressed = true;       
        }
        else if (evt == Define.MouseEvent.Click)
        {
            _currentTarget.BaseAttack(true);
            //_isMousePressed = false;
        }
    }

    // 스킬 입력 처리
    private void HandleSkillQ() => _currentTarget?.UseSkillQ();
    private void HandleSkillE() => _currentTarget?.UseSkillE();

    private void HandleInteract()
    {
        if (_currentTarget == null || _currentTarget.Stat.HealthComp.IsDead) return;

        // 현재 컨트롤 중인 캐릭터 주변에 상호작용 가능한 NPC가 있다면
        if (_currentTarget.CurrentInteractable != null)
        {
            // 상호작용 실행! (NoahController.Interact 호출됨)
            _currentTarget.CurrentInteractable.Interact();
        }
    }
    
    private async void HandleInventory()
    {
        // 팝업 열림 중엔 컨텍스트 게이트가 이 콜백 자체를 막으므로 중복 오픈 방지 분기 불필요.
        // (로딩 중 재진입은 ShowPopupUIAsync의 _isLoadingPopup 가드가 처리)
        UI_Inventory inventory = await Managers.UI.ShowPopupUIAsync<UI_Inventory>("UI_Inventory");
    }

    private async void HandleInfo()
    {
        UI_Info info = await Managers.UI.ShowPopupUIAsync<UI_Info>("UI_Info");
    }

    // Update 로직 -> Managers.Update에서 호출
    public void OnUpdate()
    {
        //if (_currentTarget == null || _currentTarget.Stat.HealthComp.IsDead) return;

        //_currentTarget.Attack(_isMousePressed);
    }

    // PlayerController.cs
    public void ClearMouseState()
    {
        _isMousePressed = false;
    }

    // 정리 메서드 (Dispose 시 호출)
    public void Dispose()
    {
        Managers.Input.OnMoveInput -= HandleMove;
        Managers.Input.MouseAction -= HandleMouse;

        // RegisterAction으로 건 콜백들도 대칭 해제 (이전엔 누락 → 재진입 시 중복 발동).
        Managers.Input.UnregisterAction(InputIntent.Info, HandleInfo);
        Managers.Input.UnregisterAction(InputIntent.Inventory, HandleInventory);
        Managers.Input.UnregisterAction(InputIntent.SkillQ, HandleSkillQ);
        Managers.Input.UnregisterAction(InputIntent.SkillE, HandleSkillE);
        Managers.Input.UnregisterAction(InputIntent.Interact, HandleInteract);
    }
}
