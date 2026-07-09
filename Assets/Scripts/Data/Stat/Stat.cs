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

    // 이 스탯 변경치의 출처 (장비 객체, 버프 스킬 객체 등)
    public object Source;

    // 생성자에 Source 추가
    public StatModifier(float value, StatModType type, object source = null)
    {
        Value = value;
        Type = type;
        Source = source;
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

    // 값이 바뀔 때(모디파이어 추가/해제, 기본값 변경) 발행. UI 실시간 갱신용.
    public event Action OnChanged;

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
        OnChanged?.Invoke();
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
        OnChanged?.Invoke();
    }

    public void RemoveModifier(StatModifier mod)
    {
        if (mod == null) return;
        if (_modifiers.Remove(mod))
        {
            _isDirty = true;
            OnChanged?.Invoke();
        }
    }

    private float CalculateFinalValue()
    {
        float sumFlat = 0f;
        float sumPercentAdd = 0f;

        // 리스트를 순회하며 부위별 합연산 수치와 곱연산 수치를 각각 따로 더해둡니다.
        foreach (StatModifier mod in _modifiers)
        {
            if (mod.Type == StatModType.Flat)
            {
                sumFlat += mod.Value;
            }
            else if (mod.Type == StatModType.PercentAdd)
            {
                sumPercentAdd += mod.Value;
            }
        }

        // [핵심 공식 수정] 기본값에 퍼센트를 먼저 곱하고, 그 뒤에 플랫(합연산) 값을 더합니다.
        float finalValue = (_baseValue * (1.0f + sumPercentAdd)) + sumFlat;

        // 소수점 4자리 반올림 처리
        return (float)Math.Round(finalValue, 4);
    }

    // 출처(Source)를 기반으로 Modifier들을 모두 찾아 삭제하는 함수
    public bool RemoveAllModifiersFromSource(object source)
    {
        bool didRemove = false;

        // 리스트를 역순으로 돌면서 source가 같은 것을 삭제
        for (int i = _modifiers.Count - 1; i >= 0; i--)
        {
            if (_modifiers[i].Source == source)
            {
                _modifiers.RemoveAt(i);
                didRemove = true;
            }
        }

        if (didRemove)
        {
            _isDirty = true;
            OnChanged?.Invoke();
        }

        return didRemove;
    }

    public void ClearModifier()
    {
        _modifiers.Clear();
        _isDirty = true;
        OnChanged?.Invoke();
    }
}
