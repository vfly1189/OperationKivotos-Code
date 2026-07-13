using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


public class AbilityContext
{
    public IAbilityCaster Caster;   // 스킬 시전자
    public GameObject CasterGO;     // 데미지 source 등 구체 참조
    public Transform Object;        // 시전 기준점 (총구 등)
    public GameObject Target;       // 대상 (없을 수도)
    public BaseStat CasterStat;     // 데미지 계산용
    public Vector3 TargetPoint;     // 착탄/조준 월드 좌표 (AoE·포격 등)
    public GameplayTagContainer Tags; // 캐스터 보유 태그 (게이트 판독·GrantsTags 부여 대상). 없으면 태그 제약 없음.

    public AbilityContext CloneAt(Vector3 point) => new AbilityContext
    {
        Caster = Caster,
        CasterGO = CasterGO,
        Object = Object,
        Target = Target,
        CasterStat = CasterStat,
        Tags = Tags,
        TargetPoint = point, //  이것만 교체
    };
}

[CreateAssetMenu(fileName = "AbilityDataSO", menuName = "Ability/AbilityData")]
public class AbilityData : ScriptableObject
{
    [Header("Cost / Timing")]
    public float Cooldown = 1f;
    public int Cost = 0;
    public float CastTime = 0f;

    [Header("Tags (게이팅)")]
    [Tooltip("캐스터가 이 태그들을 전부 보유해야 발동 (예: State.Marked 요구)")]
    public List<GameplayTagSO> RequiredTags = new();
    [Tooltip("캐스터가 이 태그를 하나라도 보유하면 발동 차단 (예: State.Stunned)")]
    public List<GameplayTagSO> BlockedTags = new();
    [Tooltip("발동(캐스트) 동안 캐스터에 부여, 이펙트 종료 시 자동 해제 (예: State.Casting)")]
    public List<GameplayTagSO> GrantsTags = new();


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
