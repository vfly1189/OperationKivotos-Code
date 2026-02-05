using System;
using UnityEngine;


// 교체 로직만 담당 (쿨타임, 유효성 검사, 실제 swap)
public class PartySwapController
{
    private PartyRegistry _registry;
    private float _swapCooldown = 1.0f;
    private float _lastSwapTime = -99f;

    public event Action<int, int> OnSwapRequested; // (prevIdx, nextIdx)

    public PartySwapController(PartyRegistry registry)
    {
        _registry = registry;
    }

    public bool CanSwap(int targetIndex)
    {
        if (Time.time - _lastSwapTime < _swapCooldown) return false;
        if (targetIndex >= _registry.Members.Count) return false;
        if (targetIndex == _registry.CurrentIndex) return false;
        if (_registry.Members[targetIndex].Stat.IsDead) return false;
        if (_registry.GetCurrent().IsUsingSkill) return false;
        return true;
    }

    public void TrySwap(int targetIndex)
    {
        if (!CanSwap(targetIndex)) return;

        int prevIdx = _registry.CurrentIndex;
        _registry.SetCurrentIndex(targetIndex);
        _lastSwapTime = Time.time;

        OnSwapRequested?.Invoke(prevIdx, targetIndex);
    }
}
