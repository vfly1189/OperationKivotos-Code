using System.Collections.Generic;
using UnityEngine;


public class AbilityContext
{
    public IAbilityCaster Caster;   // 스킬 시전자
    public GameObject CasterGO;     // 데미지 source 등 구체 참조
    public Transform Object;        // 시전 기준점 (총구 등)
    public GameObject Target;       // 대상 (없을 수도)
    public BaseStat CasterStat;     // 데미지 계산용
    public Vector3 TargetPoint;     // 착탄/조준 월드 좌표 (AoE·포격 등)
}

[CreateAssetMenu(fileName = "AbilityDataSO", menuName = "Ability/AbilityData")]
public class AbilityData : ScriptableObject
{
    [Header("Cost / Timing")]
    public float Cooldown = 1f;
    public int Cost = 0;
    public float CastTime = 0f;


    [Header("Effects (발동 시 순서대로 실행)")]
    [SerializeField] private List<EffectData> _effects = new();

    // 발동 순간, 데이터 배열 → 런타임 IEffect 리스트로 변환
    public List<IEffect> BuildRuntimeEffects()
    {
        var list = new List<IEffect>(_effects.Count);
        foreach (var data in _effects)
            if (data != null) list.Add(data.CreateRuntime());
        return list;
    }
}
