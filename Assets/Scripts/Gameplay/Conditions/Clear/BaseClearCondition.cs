using System;
using UnityEngine;

// 모든 클리어 조건의 부모 클래스
public abstract class BaseClearCondition : MonoBehaviour
{
    public event Action OnConditionMet;
    protected void InvokeConditionMet() => OnConditionMet?.Invoke();

    //public abstract void SetupCondition(GameObject mapRoot);
}
