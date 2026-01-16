using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager
{
    public Action KeyAction = null;
    public Action<Define.MouseEvent> MouseAction = null;

    bool _pressed = false;

    public void OnUpdate()
    {


        if (Keyboard.current.anyKey.isPressed && KeyAction != null)
            KeyAction.Invoke();

        if (MouseAction != null)
        {
            if (Mouse.current.rightButton.isPressed)
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

    public void Clear()
    {
        KeyAction = null;
        MouseAction = null;
    }
}
