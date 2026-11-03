
using System;
using System.Collections.Generic;
using UnityEngine;

// 하나의 동작
public abstract class PatternAction : MonoBehaviour
{
    [SerializeField] public List<PatternTask> _tasks;

    public abstract void Begin(PatternContext ctx);
    public abstract NodeState Tick(); // Running이면 진행중, Success 면 끝남
    public abstract void Cancel();

    // 신호(태그) — Task보다 먼저 받는다. 끝을 신호로 정하는 Action(AnimationAction의 _endTag)이 쓴다.
    public virtual void OnTag(GameplayTagSO tag) { }
}
