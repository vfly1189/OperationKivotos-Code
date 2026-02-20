using System.Collections.Generic;
using UnityEngine;

public class Stat
{
    // 기본값 (태생 능력치)
    [SerializeField] private float _baseValue;

    // 최종값 캐싱 (매번 계산하면 비효율적이니까)
    private float _value;
    private bool _isDirty = true; // 값이 변경되었는지 체크

    // 수정자 리스트
    private List<float> _modifiers = new List<float>();

    // 생성자
    public Stat(float baseValue = 0)
    {
        _baseValue = baseValue;
        _modifiers = new List<float>();
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
                _value = CalculateFinalValue();
                _isDirty = false;
            }
            return _value;
        }
    }

    // 수정자 추가 (장비 장착, 버프)
    public void AddModifier(float modifier)
    {
        if (modifier != 0)
        {
            _modifiers.Add(modifier);
            _isDirty = true;
        }
    }

    public void ClearModifier()
    {
        _modifiers.Clear();
        _isDirty = true;
    }

    // 수정자 제거 (장비 해제, 버프 종료)
    public void RemoveModifier(float modifier)
    {
        if (modifier != 0)
        {
            _modifiers.Remove(modifier);
            _isDirty = true;
        }
    }

    // 최종값 계산 로직
    private float CalculateFinalValue()
    {
        float finalValue = _baseValue;

        // 간단한 합연산 예시 (실무에선 합연산/곱연산 구분 필요할 수 있음)
        foreach (float mod in _modifiers)
        {
            finalValue += mod;
        }

        // (곱연산 로직이 필요하다면 여기서 처리)
        // return (float)Math.Round(finalValue, 4); // 부동소수점 오차 방지
        return finalValue;
    }
}
