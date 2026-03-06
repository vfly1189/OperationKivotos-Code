using System;
using System.Collections.Generic;
using UnityEngine;


// 최종 스탯 = (기본 스탯 + 합연산 보너스) * ( 1 + 퍼센트 보너스 )
// 스탯 변경치 타입을 정의하는 Enum
public enum StatModType
{
    Flat = 100,        // 합연산 (예: 공격력 +50)
    PercentAdd = 200   // 곱연산 (예: 공격력 +10%)
}

// 스탯 변경치를 담는 객체
[Serializable]
public class StatModifier
{
    public float Value;
    public StatModType Type;

    // 생성자
    public StatModifier(float value, StatModType type)
    {
        Value = value;
        Type = type;
    }
}



public class Stat
{
    // 기본값 (태생 능력치)
    [SerializeField] private float _baseValue;

    // 최종값 캐싱 (매번 계산하면 비효율적이니까)
    private float _finalValue;
    private bool _isDirty = true; // 값이 변경되었는지 체크

    // 수정자 리스트
    private List<StatModifier> _modifiers = new List<StatModifier>();

    // 생성자
    public Stat(float baseValue = 0)
    {
        _baseValue = baseValue;
    }

    // 기본값 설정 (레벨업 등으로 영구 스탯 변화 시)
    public void SetBaseValue(float value)
    {
        _baseValue = value;
        _isDirty = true;
    }

    // 최종값 가져오기 (외부에선 이것만 호출)
    public float Value
    {
        get
        {
            if (_isDirty)
            {
                _finalValue = CalculateFinalValue();
                _isDirty = false;
            }
            return _finalValue;
        }
    }

    // 장비나 버프를 장착할 때 Modifier 객체를 통째로 넘김
    public void AddModifier(StatModifier mod)
    {
        if (mod == null || mod.Value == 0) return;
        _modifiers.Add(mod);

        // 정렬: Flat(합연산)이 먼저 계산되고, 그 다음에 Percent(곱연산)가 오도록 정렬
        _modifiers.Sort((a, b) => a.Type.CompareTo(b.Type));
        _isDirty = true;
    }

    public void RemoveModifier(StatModifier mod)
    {
        if (mod == null) return;
        if (_modifiers.Remove(mod))
        {
            _isDirty = true;
        }
    }

    private float CalculateFinalValue()
    {
        float finalValue = _baseValue;
        float sumPercentAdd = 0f;

        // 리스트를 순회하며 계산
        // 정렬해두었기 때문에 Flat(합연산)이 모두 먼저 처리되고, 그 다음 PercentAdd가 묶여서 처리됨
        foreach (StatModifier mod in _modifiers)
        {
            if (mod.Type == StatModType.Flat)
            {
                finalValue += mod.Value;
            }
            else if (mod.Type == StatModType.PercentAdd)
            {
                sumPercentAdd += mod.Value;
            }
        }

        // 마지막으로 퍼센트 수치 적용 (예: 0.1 이면 10% 증가)
        finalValue *= (1.0f + sumPercentAdd);

        // 소수점 4자리 반올림 처리 (부동소수점 오류 방지)
        return (float)Math.Round(finalValue, 4);
    }


    public void ClearModifier()
    {
        _modifiers.Clear();
        _isDirty = true;
    }
}
