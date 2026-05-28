using System;
using UnityEngine;

public class PartySwapController
{
    private readonly PartyRegistry _registry;
    private readonly float _swapCooldown = 1.0f;
    private float _lastSwapTime = -99f;

    public event Action<int, int> OnSwapRequested;

    public PartySwapController(PartyRegistry registry)
    {
        this._registry = registry;
    }

    public bool CanSwap(int targetIndex)
    {
        if (Time.time - _lastSwapTime < _swapCooldown) return false;
        if (targetIndex >= _registry.Members.Count) return false;
        if (targetIndex == _registry.CurrentIndex) return false;
        if (_registry.Members[targetIndex].Stat.IsDead) return false;

        var current = _registry.GetCurrent();
        if (current.IsUsingSkill) return false;
        if (!current.CanSwap) return false; // 공격(Attack) 상태 차단

        return true;
    }

    // isForce: 사망 처리 후 강제 스왑 등 내부용
    public void TrySwap(int targetIndex, bool isForce = false)
    {
        if (!isForce && !CanSwap(targetIndex)) return;
        if (_registry.Members[targetIndex].Stat.IsDead) return;

        int prevIdx = _registry.CurrentIndex;
        _registry.SetCurrentIndex(targetIndex);
        _lastSwapTime = Time.time;
        OnSwapRequested?.Invoke(prevIdx, targetIndex);
    }
}