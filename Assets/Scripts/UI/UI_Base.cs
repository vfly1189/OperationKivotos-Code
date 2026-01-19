using UnityEngine;

public abstract class UI_Base : MonoBehaviour
{
    public abstract void Init();

    protected virtual void Start()
    {
        Init();
    }
}
