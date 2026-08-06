using System;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class CharacterStat : BaseStat
{
    [Header("Data")]
    [SerializeField] private CharacterDataSO _data; // 초기 데이터

    [Header("Equipment")]
    [SerializeField] private WeaponDataSO _weaponData; // 인스펙터에서 캐릭터별로 할당
    public WeaponDataSO WeaponData => _weaponData;


    // 계산된 스탯들
    public Stat MaxEnergy;
    public Stat CritRate => GetStat(EStatType.CritRate);
    public Stat CritDamage => GetStat(EStatType.CritDamage);
    public Stat MoveSpeed => GetStat(EStatType.MoveSpeed);
    public Stat EnergyRecharge => GetStat(EStatType.EnergyRegen);

    public int WeaponLevel { get; private set; } = 1;
    public float CurrentEnergy { get; private set; }

    // 무적/HP/사망 상태는 Health가 소유(HealthComp). 캐릭터 상태 복원 시 HP 부분만 HealthComp로 위임한다.
    // 스킬 쿨타임은 AbilityRunner(캐스터별, 절대시각)가 소유한다. 궁 준비 판정은 BaseCharacter.IsUltimateReady를 UI가 폴링.

    public event Action<float, float> OnEnergyChanged;  // cur, max

    public override void Init()
    {
        base.Init();

        // 초기화
        MaxEnergy = new Stat();

        // 자식만의 고유 스탯들을 딕셔너리에 추가
        _stats[EStatType.EnergyRegen] = new Stat();
        _stats[EStatType.CritRate] = new Stat();
        _stats[EStatType.CritDamage] = new Stat();
        _stats[EStatType.MoveSpeed] = new Stat();

        if (_data != null) SetCharacterData(_data);

        ApplyWeaponStats();
    }
    void Awake()
    {

    }
    public void SetCharacterData(CharacterDataSO data)
    {
        _data = data;

        UpdateBaseStatsByPartyLevel(Managers.Party.PartyLevel);

        // 고정 스탯 세팅
        MaxEnergy.SetBaseValue(data.maxEnergy);
        CritRate.SetBaseValue(data.baseCritRate);
        CritDamage.SetBaseValue(data.baseCritDamage);
        MoveSpeed.SetBaseValue(data.baseMoveSpeed);
        EnergyRecharge.SetBaseValue(data.baseEnergyRecharge);

        // 실시간 수치 풀충전
        HealthComp.SetHpRaw(MaxHp.Value);
        CurrentEnergy = 0;
    }
    public void UpdateBaseStatsByPartyLevel(int partyLevel)
    {
        MaxHp.SetBaseValue(_data.GetLevelHp(partyLevel));
        Attack.SetBaseValue(_data.GetLevelAttack(partyLevel));
        Defense.SetBaseValue(_data.GetLevelDefense(partyLevel));
    }

    public CharacterDataSO GetData() { return _data; }
    public string GetNameKey() { return _data.nameKey; }
    //public AssetReferenceSprite GetPortrait() { return _data.Portrait; }
    public AssetReferenceGameObject GetSelectModel() { return _data.selectPrefab; }

    public int GetID() { return _data.id; }

    public void ResetState()
    {
        HealthComp.SetDead(false);
        HealthComp.IsInvincible = false;
        HealthComp.SetHpRaw(MaxHp.Value);
        CurrentEnergy = 0;

        // Collider 비활성화 (추가 피격 방지)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Rigidbody rigid = GetComponent<Rigidbody>();
        if (rigid != null) rigid.isKinematic = false;

        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
        HealthComp.RaiseHpChanged(HealthComp.CurrentHp, MaxHp.Value);
    }

    // 마을 귀환용 상태 보정 (생존자는 체력 유지, 사망자만 예외 부활)
    public void ReturnToTownState()
    {
        // 사망한 캐릭터라면 HP 1로 예외 부활
        if (HealthComp.CurrentHp <= 0 || HealthComp.IsDead)
        {
            HealthComp.SetDead(false);
            HealthComp.SetHpRaw(1);
        }
        else
        {
            // 생존자는 체력 유지 (최대 체력 넘어가지 않게 방지)
            HealthComp.SetHpRaw(Mathf.Clamp(HealthComp.CurrentHp, 0, MaxHp.Value));
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Rigidbody rigid = GetComponent<Rigidbody>();
        if (rigid != null) rigid.isKinematic = false;

        HealthComp.IsInvincible = false; // 무적은 무조건 해제
        CurrentEnergy = 0;

        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
        HealthComp.RaiseHpChanged(HealthComp.CurrentHp, MaxHp.Value);
    }

    //테스트용 — 에너지 자동 충전 (스킬 쿨타임은 AbilityRunner가 절대시각으로 소유하므로 여기서 tick하지 않는다)
    private void Update()
    {
        float prevEnergy = CurrentEnergy;
        CurrentEnergy = Mathf.Min(CurrentEnergy + 10.0f * Time.deltaTime, MaxEnergy.Value);

        if (!Mathf.Approximately(prevEnergy, CurrentEnergy))
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
    }

    // 전투 시스템 등 외부에서 호출해줘야 함
    public void AddEnergy(float amount)
    {
        if (CurrentEnergy >= MaxEnergy.Value) return;

        CurrentEnergy += amount;
        if (CurrentEnergy > MaxEnergy.Value) CurrentEnergy = MaxEnergy.Value;

        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
    }

    // 궁극기(Q) 발동 시 궁게이지 전량 소비. 쿨은 AbilityRunner가 소유하므로 여기선 에너지만 다룬다.
    public void ConsumeUltimateGauge()
    {
        CurrentEnergy = 0;
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
    }

    public AssetReferenceT<AudioClip>[] GetBattleInVoice()
    {
        return _data.battleInVoices;
    }

    public AssetReferenceT<AudioClip>[] GetBattleVictoryVoices()
    {
        return _data.battleVictoryVocies;
    }

    // 발사 데미지 조립 + 치명타 롤. (기존 BaseCharacter.CalculatedDamage 로직을 Stat으로 이관)
    public override DamageInfo BuildOutgoingDamage(GameObject attacker)
    {
        bool isCrit = UnityEngine.Random.value < CritRate.Value;
        float finalDamage = isCrit ? Attack.Value * CritDamage.Value : Attack.Value;
        return new DamageInfo(finalDamage, attacker, isCrit);
    }

    // 피격(방어력·HP차감·데미지표시)·사망은 Health가 통합 처리한다(Step 2).
    // 사망 시 collider 비활성화 등 캐릭터 고유 반응은 BaseCharacter가 Health.OnDead를 구독해 수행.

    private void ApplyWeaponStats()
    {
        if (_weaponData == null) return;

        // 1. 기존에 적용된 무기 보정치 초기화
        Attack.RemoveAllModifiersFromSource(_weaponData);
        MaxHp.RemoveAllModifiersFromSource(_weaponData);
        CritRate.RemoveAllModifiersFromSource(_weaponData);
        CritDamage.RemoveAllModifiersFromSource(_weaponData);

        WeaponLevelStat wStat = _weaponData.GetStatByLevel(WeaponLevel);

        // 3. Stat 클래스에 합연산(Flat)으로 더해줌
        Attack.AddModifier(new StatModifier(wStat.Attack, StatModType.Flat, _weaponData));
        MaxHp.AddModifier(new StatModifier(wStat.HP, StatModType.Flat, _weaponData));
        CritRate.AddModifier(new StatModifier(wStat.CritRate, StatModType.Flat, _weaponData));
        CritDamage.AddModifier(new StatModifier(wStat.CritDmg, StatModType.Flat, _weaponData));

        HealthComp.RaiseHpChanged(HealthComp.CurrentHp, MaxHp.Value);

        GameLog.Log($"[{_data.nameKR}] 무기({_weaponData.weaponName}) Lv.{WeaponLevel} 스탯 적용 완료");
    }

    public void ApplyCharacterSaveData(CharacterSaveData saved)
    {
        // 무기 레벨 복원
        WeaponLevel = saved.weaponLevel;
        ApplyWeaponStats();

        // HP 복원 (마을 귀환 시 풀충전 원하면 MaxHp.Value로 변경)
        HealthComp.SetHpRaw(Mathf.Clamp(saved.currentHp, 0, MaxHp.Value));
        if (HealthComp.CurrentHp > 0) HealthComp.SetDead(false);

        HealthComp.RaiseHpChanged(HealthComp.CurrentHp, MaxHp.Value);
    }
    public void WeaponLevelUp()
    {
        WeaponLevel++;

        ApplyWeaponStats();
    }


    public void RefreshStatsUI()
    {
        // 1. 필요한 내부 로직 처리 (최대 체력이 변했을 수 있으니 현재 체력 보정)
        HealthComp.SetHpRaw(Mathf.Clamp(HealthComp.CurrentHp, 0, MaxHp.Value));

        // 2. Health가 HP 변경 이벤트를 발생시킴
        HealthComp.RaiseHpChanged(HealthComp.CurrentHp, MaxHp.Value);
    }
}
