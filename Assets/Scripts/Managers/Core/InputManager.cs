using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager
{
    // 고정 키 (자주 사용, 파라미터 있음)
    public event Action OnEscapePressed;        //ESC
    public event Action<Vector2> OnMoveInput;   //이동 <- 나중에 바꿀수도 있음
    public event Action<Define.MouseEvent> MouseAction;

    bool _pressed = false;

    // 입력 컨텍스트 스택 (Gameplay/UI). UIManager가 팝업 열고닫을 때 Push/Pop 구동.
    private readonly InputContextStack _context = new InputContextStack();

    public InputContext CurrentContext => _context.Current;
    public void PushContext(InputContext ctx) => _context.Push(ctx);
    public void PopContext() => _context.Pop();

    // 컨텍스트별 "허용 Intent" 집합 (Unity Action Map 대응).
    // Gameplay는 전부 허용이라 명시 안 함. UI는 아래 집합만 통과(이동/스왑 O, 전투·상호작용 X).
    private static readonly Dictionary<InputContext, HashSet<InputIntent>> _contextAllow =
        new Dictionary<InputContext, HashSet<InputIntent>>
        {
            [InputContext.UI] = new HashSet<InputIntent>
            {
                InputIntent.Move,
                InputIntent.Swap1, InputIntent.Swap2, InputIntent.Swap3, InputIntent.Swap4,
            },
        };

    // 현재 컨텍스트에서 이 Intent가 통과되는가.
    private bool IsAllowed(InputIntent intent)
    {
        if (_context.IsGameplay) return true;                       // Gameplay: 전부 허용
        return _contextAllow.TryGetValue(_context.Current, out var set) && set.Contains(intent);
    }

    // 동적 키 (리맵핑 가능) — Intent → 실제 Key
    private Dictionary<InputIntent, Key> _keyMap = new Dictionary<InputIntent, Key>()
    {
        { InputIntent.Info,      Key.T },
        { InputIntent.Inventory, Key.I },
        { InputIntent.Interact,  Key.F },
        { InputIntent.SkillE,    Key.E },
        { InputIntent.SkillQ,    Key.Q },
        { InputIntent.Swap1,     Key.Digit1 },
        { InputIntent.Swap2,     Key.Digit2 },
        { InputIntent.Swap3,     Key.Digit3 },
        { InputIntent.Swap4,     Key.Digit4 }
    };

    private Dictionary<InputIntent, Action> _actionMap = new Dictionary<InputIntent, Action>();

    public void OnUpdate()
    {
        if (Keyboard.current == null) return; // 키보드 연결 체크

        // 고정 키 (최적화) — ESC는 컨텍스트 무관(팝업 닫기/메뉴 열기)이라 게이트 앞에서 처리
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            OnEscapePressed?.Invoke();

        // 이동 입력 (WASD) — 허용 컨텍스트면 읽고, 아니면 (0,0)으로 정지시킴
        if (OnMoveInput != null)
        {
            Vector2 moveDir = Vector2.zero;
            if (IsAllowed(InputIntent.Move))
            {
                if (Keyboard.current.wKey.isPressed) moveDir.y += 1;
                if (Keyboard.current.sKey.isPressed) moveDir.y -= 1;
                if (Keyboard.current.aKey.isPressed) moveDir.x -= 1;
                if (Keyboard.current.dKey.isPressed) moveDir.x += 1;
            }
            // 차단 컨텍스트에선 zero → 정지 (입력이 없어도 (0,0)을 보내야 멈춤)
            OnMoveInput.Invoke(moveDir.normalized);
        }

        // 동적 키 (유연성) — 현재 컨텍스트에서 허용된 Intent만 발화
        foreach (var pair in _actionMap)
        {
            if (!IsAllowed(pair.Key)) continue;
            if (_keyMap.TryGetValue(pair.Key, out Key key) && Keyboard.current[key].wasPressedThisFrame)
                pair.Value?.Invoke();
        }

        // 평타 (마우스) — 허용 컨텍스트만. 차단되면 눌림 상태를 자동 해제(Click)한다.
        if (MouseAction != null)
        {
            if (IsAllowed(InputIntent.Attack) && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                MouseAction.Invoke(Define.MouseEvent.Press);
                _pressed = true;
            }
            else if (_pressed)
            {
                MouseAction.Invoke(Define.MouseEvent.Click);
                _pressed = false;
            }
        }
    }

    // Intent에 할당된 키의 표시 문자열을 반환 ("F", "1", "Tab" 등)
    public string GetKeyName(InputIntent intent)
    {
        if (_keyMap.TryGetValue(intent, out Key key))
        {
            if (Keyboard.current != null)
            {
                // New Input System에서 제공하는 깔끔한 문자열 변환 기능 (예: Key.Digit1 -> "1")
                return Keyboard.current[key].displayName;
            }

            // 키보드가 연결 안 된 예외 상황 시 Enum 이름 그대로 반환
            return key.ToString();
        }
        return "?";
    }

    // 동적 키 등록 (Intent 기반)
    public void RegisterAction(InputIntent intent, Action callback)
    {
        if (_actionMap.ContainsKey(intent))
            _actionMap[intent] += callback;
        else
            _actionMap[intent] = callback;
    }

    public void UnregisterAction(InputIntent intent, Action callback)
    {
        if (_actionMap.ContainsKey(intent))
            _actionMap[intent] -= callback;
    }

    // 키 리맵핑
    public void RemapKey(InputIntent intent, Key newKey)
    {
        if (_keyMap.ContainsKey(intent))
            _keyMap[intent] = newKey;
    }

    public Key GetKey(InputIntent intent)
    {
        return _keyMap.TryGetValue(intent, out Key key) ? key : Key.None;
    }

    public void Clear()
    {
        //_keyMap.Clear();
        _actionMap.Clear();
        _context.Clear();              // 씬 전환 시 컨텍스트 잔류 방지

        OnEscapePressed = null;        //ESC
        OnMoveInput = null;            //이동
        MouseAction = null;
    }
}
