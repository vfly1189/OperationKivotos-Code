using NPOI.SS.UserModel.Charts;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class CharacterStat : BaseStat, IDamageable
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

    public Stat QSkillCoolTime;
    public Stat ESkillCoolTime;


    public int WeaponLevel { get; private set; } = 1;
    public float CurrentEnergy { get; private set; }
    public float CurrentQSkillCoolTime { get; private set; }
    public float CurrentESkillCoolTime { get; private set; }

    public bool IsInvincible { get; set; } = false; //무적인지

    private bool _isUltimateReady = false;


    public event Action<float, float> OnEnergyChanged;  // cur, max
    public event Action<bool> OnUltimateStateChanged;   // isReady

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

        QSkillCoolTime = new Stat();
        ESkillCoolTime = new Stat();

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

        QSkillCoolTime.SetBaseValue(data.QSkillCoolTime);
        ESkillCoolTime.SetBaseValue(data.ESkillCoolTime);

        // 실시간 수치 풀충전
        CurrentHp = MaxHp.Value; 
        CurrentEnergy = 0;
        CurrentQSkillCoolTime = QSkillCoolTime.Value;
        CurrentESkillCoolTime = 0;
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
        IsDead = false;

        CurrentHp = MaxHp.Value;
        CurrentEnergy = 0;
        CurrentQSkillCoolTime = QSkillCoolTime.Value;
        CurrentESkillCoolTime = 0;

        // Collider 비활성화 (추가 피격 방지)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Rigidbody rigid = GetComponent<Rigidbody>();
        if (rigid != null) rigid.isKinematic = false;
    }

    //테스트용
    private void Update()
    {
        CoolTimeUpdate();

        float prevEnergy = CurrentEnergy;
        CurrentEnergy = Mathf.Min(CurrentEnergy + 10.0f * Time.deltaTime, MaxEnergy.Value);

        if (!Mathf.Approximately(prevEnergy, CurrentEnergy))
        {
            OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
            CheckUltimateReadyState();
        }
    }


    private void CoolTimeUpdate()
    {
        // Q 스킬 쿨타임
        if (CurrentQSkillCoolTime > 0)
        {
            CurrentQSkillCoolTime -= Time.deltaTime;
            if (CurrentQSkillCoolTime <= 0)
            {
                CurrentQSkillCoolTime = 0;
                CheckUltimateReadyState();
            }
        }

        // E 스킬 쿨타임
        if (CurrentESkillCoolTime > 0)
            CurrentESkillCoolTime -= Time.deltaTime;
    }

    // 전투 시스템 등 외부에서 호출해줘야 함
    public void AddEnergy(float amount)
    {
        if (CurrentEnergy >= MaxEnergy.Value) return;

        CurrentEnergy += amount;
        if (CurrentEnergy > MaxEnergy.Value) CurrentEnergy = MaxEnergy.Value;

        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
        CheckUltimateReadyState(); // 에너지 찼으니 궁극기 상태 체크
    }

   
    public bool TryUseSkillQ()
    {
        if (CurrentQSkillCoolTime > 0) return false;
        if (CurrentEnergy < MaxEnergy.Value) return false;

        CurrentEnergy = 0;
        CurrentQSkillCoolTime = QSkillCoolTime.Value;

        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
        CheckUltimateReadyState();
        return true;
    }

    public bool TryUseSkillE()
    {
        if (CurrentESkillCoolTime > 0) return false;
        CurrentESkillCoolTime = 0;
        return true;
    }

    private void CheckUltimateReadyState()
    {
        // 준비 완료 조건: (쿨타임 0) 그리고 (에너지 가득 참)
        bool isNowReady = (CurrentQSkillCoolTime <= 0) && (CurrentEnergy >= MaxEnergy.Value);

        if (_isUltimateReady != isNowReady)
        {
            _isUltimateReady = isNowReady;
            OnUltimateStateChanged?.Invoke(_isUltimateReady); // UI야, 상태 바꼈다!
        }
    }   
    public AssetReferenceT<AudioClip>[] GetBattleInVoice()
    {
        return _data.battleInVoices;
    }

    public AssetReferenceT<AudioClip>[] GetBattleVictoryVoices()
    {
        return _data.battleVictoryVocies;
    }

    public override void TakeDamage(DamageInfo damageInfo)
    {
        // 1. 무적 체크: 무적이면 데미지 연산 스킵
        if (IsInvincible)
        {
            Debug.Log("무적 상태라 데미지를 입지 않습니다.");
            // (선택) 여기서 "IMMUNE" 같은 텍스트 이펙트를 띄우기
            return;
        }
        if (IsDead) return;

        float finalDamage = Mathf.Max(damageInfo.Amount - Defense.Value, 1);
        CurrentHp -= finalDamage;
        CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp.Value);
        // 피격 이펙트, 사운드 처리 등을 damageInfo.HitPoint를 활용해 여기서 처리 가능

        if (CurrentHp <= 0) HandleDeath(damageInfo.Attacker);

        ToastDamageUI(finalDamage, damageInfo.HitPoint, true);
        CallOnHpChanged(CurrentHp, MaxHp.Value);
    }

    protected override void HandleDeath(GameObject shooter)
    {
        if (IsDead) return; // 중복 호출 방지

        IsDead = true;

        // Collider 비활성화 (추가 피격 방지)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        CallOnDead();
    }

    public void ResetStat()
    {
        CurrentHp = MaxHp.Value;
    }


    // [추가] 런타임 데이터 적용 함수 (Restore)
    public void ApplyRuntimeData(CharacterRuntimeData savedData)
    {
        //if (savedData == null) return;

        //// [핵심 1] 데이터를 미리 로컬 변수(값)로 복사해둠 (참조 오염 방지)
        //int targetLevel = savedData.level;
        //float targetExp = savedData.currentExp;
        //int targetWeaponLevel = savedData.weaponLevel;

        //// 1. 레벨 복구 (레벨업 로직을 반복 수행해서 스탯 뻥튀기)
        //// 현재 1레벨이므로 (savedData.level - 1)번 레벨업
        //for (int i = 1; i < savedData.level; i++)
        //{
        //    LevelUp(true); // 이 함수 안에서 스탯 증가가 일어남
        //}

        //WeaponLevel = targetWeaponLevel;
        //ApplyWeaponStats();

        //// 2. 경험치 복구
        //CurrentExp = targetExp;

        //// 3. 체력/에너지는 풀로 채워주기 (마을 귀환 서비스)
        //CurrentHp = MaxHp.Value;
        //CurrentEnergy = 0; // 또는 MaxEnergy

        //// 4. UI 및 데이터 갱신을 여기서 한 번만 수행
        //OnLevelChanged?.Invoke((int)CurLevel);
        //OnExpChanged?.Invoke(CurrentExp, MaxExp);
        //OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
        //CallOnHpChanged(CurrentHp, MaxHp.Value);
    }

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

        CallOnHpChanged(CurrentHp, MaxHp.Value);

        Debug.Log($"[{_data.nameKR}] 무기({_weaponData.weaponName}) Lv.{WeaponLevel} 스탯 적용 완료");
    }

    public void ApplyCharacterSaveData(CharacterSaveData saved)
    {
        // 무기 레벨 복원
        WeaponLevel = saved.weaponLevel;
        ApplyWeaponStats();

        // HP 복원 (마을 귀환 시 풀충전 원하면 MaxHp.Value로 변경)
        CurrentHp = Mathf.Clamp(saved.currentHp, 0, MaxHp.Value);
        CallOnHpChanged(CurrentHp, MaxHp.Value);
    }
    public void WeaponLevelUp()
    {
        WeaponLevel++;

        ApplyWeaponStats();
    }

    // 외부(EquipmentManager 등)에서는 이 public 함수만 호출합니다.
    public void RefreshStatsUI()
    {
        // 1. 필요한 내부 로직 처리 (최대 체력이 변했을 수 있으니 현재 체력 보정)
        CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp.Value);

        // 2. 내부에서 안전하게 protected 함수를 호출하여 이벤트를 발생시킴
        CallOnHpChanged(CurrentHp, MaxHp.Value);
    }
}
