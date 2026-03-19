using UnityEngine;

public abstract class UI_Base : MonoBehaviour
{
    public abstract void Init();

    public virtual void Refresh() { }

    protected virtual void Start()
    {
        Init();
    }
}
