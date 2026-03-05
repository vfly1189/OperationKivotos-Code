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

    // 동적 키 (리맵핑 가능)
    private Dictionary<string, Key> _keyMap = new Dictionary<string, Key>()
    {
        {"Inventory", Key.I },
        {"Interact",Key.F },
        { "E_Skill", Key.E },
        { "Q_Skill", Key.Q },
        { "Swap_1", Key.Digit1 },
        { "Swap_2", Key.Digit2 },
        { "Swap_3", Key.Digit3 },
        { "Swap_4", Key.Digit4 }
    };

    private Dictionary<string, Action> _actionMap = new Dictionary<string, Action>();

    public void OnUpdate()
    {
        if (Keyboard.current == null) return; // 키보드 연결 체크

        // 고정 키 (최적화)
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            OnEscapePressed?.Invoke();

        // 2. 이동 입력 (WASD) 체크 -> BasePlayerController로 전송
        if (OnMoveInput != null)
        {
            Vector2 moveDir = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) moveDir.y += 1;
            if (Keyboard.current.sKey.isPressed) moveDir.y -= 1;
            if (Keyboard.current.aKey.isPressed) moveDir.x -= 1;
            if (Keyboard.current.dKey.isPressed) moveDir.x += 1;

            // 입력이 없어도 (0,0)을 보내야 멈출 수 있음
            OnMoveInput.Invoke(moveDir.normalized);
        }

        // 동적 키 (유연성)
        foreach (var pair in _actionMap)
        {
            if (_keyMap.TryGetValue(pair.Key, out Key key))
            {
                if (Keyboard.current[key].wasPressedThisFrame)
                {
                    pair.Value?.Invoke();
                }
            }
        }

        if (MouseAction != null)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                MouseAction.Invoke(Define.MouseEvent.Press);
                _pressed = true;
            }
            else
            {
                if (_pressed)
                {
                    MouseAction.Invoke(Define.MouseEvent.Click);
                    _pressed = false;
                }
            }
        }
    }

    // 동적 키 등록 (이름 기반)
    public void RegisterAction(string actionName, Action callback)
    {
        if (_actionMap.ContainsKey(actionName))
            _actionMap[actionName] += callback;
        else
            _actionMap[actionName] = callback;
    }

    public void UnregisterAction(string actionName, Action callback)
    {
        if (_actionMap.ContainsKey(actionName))
            _actionMap[actionName] -= callback;
    }

    // 키 리맵핑
    public void RemapKey(string actionName, Key newKey)
    {
        if (_keyMap.ContainsKey(actionName))
            _keyMap[actionName] = newKey;
    }

    public Key GetKey(string actionName)
    {
        return _keyMap.TryGetValue(actionName, out Key key) ? key : Key.None;
    }

    public void Clear()
    {
        //_keyMap.Clear();
        _actionMap.Clear();

        OnEscapePressed = null;        //ESC
        OnMoveInput = null;            //이동
        MouseAction = null;
    }
}
