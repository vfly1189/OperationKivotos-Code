using System;
using UnityEngine;

public abstract class BaseCondition : MonoBehaviour
{
    public event Action OnConditionMet;

    protected void InvokeConditionMet()
    {
        OnConditionMet?.Invoke();
    }

    public abstract void SetUp();
}
