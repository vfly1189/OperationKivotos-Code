using UnityEngine;
using UnityEngine.InputSystem;

public class StartScene : BaseScene
{
    // Update is called once per frame
    void Update()
    {
        if(Keyboard.current.qKey.isPressed)
        {
            Managers.Scene.LoadScene(Define.Scene.Select);
        }
    }

    public override void Clear()
    {

    }
}
