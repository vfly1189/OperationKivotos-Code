using System;
using UnityEngine;

public class PartyProgress
{
    public int Level { get; private set; } = 1;
    public float CurrentExp { get; private set; }
    public float RequiredExp { get; private set; }

    public event Action<int> OnLevelChanged;
    public event Action<float, float> OnExpChanged;

    public void InitFromData(int level, float currentExp)
    {
        Level = Mathf.Max(1, level);
        CurrentExp = Mathf.Max(0, currentExp);
        UpdateRequiredExp();
        OnExpChanged?.Invoke(CurrentExp, RequiredExp);
        OnLevelChanged?.Invoke(Level);
    }

    public void AddExp(float amount)
    {
        if (amount <= 0) return;

        CurrentExp += amount;
        while (RequiredExp > 0 && CurrentExp >= RequiredExp)
        {
            CurrentExp -= RequiredExp;
            Level++;
            UpdateRequiredExp();
            OnLevelChanged?.Invoke(Level);
        }

        OnExpChanged?.Invoke(CurrentExp, RequiredExp);
    }

    private void UpdateRequiredExp()
    {
        var data = Managers.Data.GetData<int, LevelExpData>(Level);
        RequiredExp = data != null ? data.RequireExp : 0f;
    }
}
