using System;
using UnityEngine;


public class CharacterStat : BaseStat, IDamageable
{
    [Header("Data")]
    [SerializeField] private CharacterDataSO _data; // 초기 데이터

    // 계산된 스탯들
    public Stat MaxEnergy;
    public Stat MaxExp;
    public Stat QSkillCoolTime;
    public Stat ESkillCoolTime;

    // 실시간 변동 스탯들
    public float CurLevel {  get; private set; }
    public float CurrentEnergy { get; private set; }
    public float CurrentExp { get; private set; }
    public float CurrentQSkillCoolTime { get; private set; }
    public float CurrentESkillCoolTime { get; private set; }

    public bool IsInvincible { get; set; } = false; //무적인지

    private bool _isUltimateReady = false;

    // 액션들 (UI 갱신용)
    //public event Action<float, float> OnHpChanged;      // cur, max
    public event Action<float, float> OnExpChanged;     // cur, max
    public event Action<int> OnLevelChanged;            // level
    public event Action<float, float> OnEnergyChanged;  // cur, max
    public event Action<bool> OnUltimateStateChanged;   // isReady
    public override void Init()
    {
        base.Init();

        // 초기화
        MaxEnergy = new Stat();
        MaxExp = new Stat();
        QSkillCoolTime = new Stat();
        ESkillCoolTime = new Stat();

        if (_data != null) SetCharacterData(_data);
    }
    void Awake()
    {
          
    }
    public void SetCharacterData(CharacterDataSO data)
    {
        _data = data;

        // 기본값 세팅
        MaxHp.SetBaseValue(data.MaxHp);
        Attack.SetBaseValue(data.Attack);
        Defense.SetBaseValue(data.Defense);
        MaxEnergy.SetBaseValue(data.MaxEnergy);
        MaxExp.SetBaseValue(data.MaxExp);
        QSkillCoolTime.SetBaseValue(data.QSkillCoolTime);
        ESkillCoolTime.SetBaseValue(data.ESkillCoolTime);

        CurLevel = 1;
        CurrentHp = MaxHp.Value; // 체력 풀로 채우기
        CurrentEnergy = 0;
        CurrentQSkillCoolTime = QSkillCoolTime.Value;
        CurrentESkillCoolTime = 0;
        CurrentExp = 0;
    }


    //테스트용
    private void Update()
    {
        CoolTimeUpdate();


        CurrentEnergy += 10.0f * Time.deltaTime;

        if (CurrentEnergy >= MaxEnergy.Value)
        {
            CurrentEnergy = MaxEnergy.Value;
        }
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
        CheckUltimateReadyState();
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

    public void AddExp(float amount)
    {
        // 1. 경험치 추가
        CurrentExp += amount;

        // 2. 레벨업 체크 (한 번에 많은 경험치를 얻어 여러 번 레벨업 할 수도 있으므로 while 사용)
        // MaxExp.Value가 0이면 무한루프 돌 수 있으니 안전장치 추가 (> 0)
        while (MaxExp.Value > 0 && CurrentExp >= MaxExp.Value)
        {
            CurrentExp -= MaxExp.Value; // 남은 경험치 이월
            LevelUp();                  // 레벨업 (여기서 MaxExp.Value가 커짐)
        }

        // 3. UI 갱신 (레벨업 후 남은 경험치 or 단순히 오른 경험치 반영)
        OnExpChanged?.Invoke(CurrentExp, MaxExp.Value);

        Debug.Log($"[Exp] Added {amount}. Current: {CurrentExp}/{MaxExp.Value}, Level: {CurLevel}");
        Managers.Context.SaveCharacterStat(_data.id, (int)CurLevel, CurrentExp);
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
    public void LevelUp()
    {
        CurLevel++;

        // 1. 새로운 기초 스탯 계산 (공식: 1레벨 스탯 + (성장치 * (현재레벨 - 1)))
        // 1레벨일 때는 성장치가 0번 적용, 2레벨일 때는 1번 적용되는 식입니다.
        float newMaxHpBase = _data.MaxHp + (_data.MaxHpGrowth * (CurLevel - 1));
        float newAttackBase = _data.Attack + (_data.AttackGrowth * (CurLevel - 1));
        float newDefenseBase = _data.Defense + (_data.DefenseGrowth * (CurLevel - 1));
        float newMaxExpBase = _data.MaxExp + (_data.ExpGrowth * (CurLevel - 1));

        Debug.Log($"새로운 최대체력 : {newMaxHpBase}");

        // 2. Stat 클래스의 BaseValue 업데이트
        // (이 함수를 호출하면 Stat 내부의 _isDirty가 true가 되어 다음 Value 호출 시 재계산됨)
        MaxHp.SetBaseValue(newMaxHpBase);
        Attack.SetBaseValue(newAttackBase);
        Defense.SetBaseValue(newDefenseBase);
        MaxExp.SetBaseValue(newMaxExpBase);

        // 3. 변동된 수치에 따른 후처리 (선택 사항)
        // 예: 최대 체력이 늘어났으니, 늘어난 만큼 현재 체력도 채워준다.
        // 혹은 레벨업 시 체/스테미너를 100% 회복시켜준다.

        // (옵션 A) 늘어난 최대치만큼 현재 체력 회복
        // float hpDiff = newMaxHpBase - (_data.MaxHp + (_data.MaxHpGrowth * (CurLevel - 2)));
        // CurrentHp += hpDiff; 

        // (옵션 B) 레벨업 시 풀 회복 (Eternal Return 등 많은 게임 방식)
        CurrentHp = MaxHp.Value;
        CurrentEnergy = MaxEnergy.Value;

        // 4. UI 갱신 알림
        OnLevelChanged?.Invoke((int)CurLevel);
        //OnHpChanged?.Invoke(CurrentHp, MaxHp.Value);
        CallOnHpChanged(CurrentHp, MaxHp.Value);
        OnExpChanged?.Invoke(CurrentExp, MaxExp.Value);
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);

        Debug.Log($"Level Up! Current Level: {CurLevel}, New Attack: {Attack.Value}");
        Managers.Context.SaveCharacterStat(_data.id, (int)CurLevel, CurrentExp);
    }

    public AudioClip[] GetBattleInVoice()
    {
        return _data.battleInVoices;
    }

    public AudioClip[] GetBattleVictoryVoices()
    {
        return _data.battleVictoryVocies;
    }

    public override void TakeDamage(DamageInfo damageInfo)
    {
        // 1. 무적 체크: 무적이면 데미지 연산 스킵
        if (IsInvincible)
        {
            Debug.Log("무적 상태라 데미지를 입지 않습니다.");
            // (선택) 여기서 "IMMUNE" 같은 텍스트 이펙트를 띄우면 좋습니다.
            return;
        }

        float finalDamage = Mathf.Max(damageInfo.Amount - Defense.Value, 1);
        CurrentHp -= finalDamage;
        CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp.Value);
        // 피격 이펙트, 사운드 처리 등을 damageInfo.HitPoint를 활용해 여기서 처리 가능

        if (CurrentHp <= 0) HandleDeath(damageInfo.Attacker);

        CallOnHpChanged(CurrentHp, MaxHp.Value);
    }

    protected override void HandleDeath(GameObject shooter)
    {
        CallOnDead();
    }

    public void ResetStat()
    {
        CurrentHp = MaxHp.Value;
    }


    // [추가] 런타임 데이터 적용 함수 (Restore)
    public void ApplyRuntimeData(CharacterRuntimeData savedData)
    {
        if (savedData == null) return;

        // 1. 레벨 복구 (레벨업 로직을 반복 수행해서 스탯 뻥튀기)
        // 현재 1레벨이므로 (savedData.level - 1)번 레벨업
        for (int i = 1; i < savedData.level; i++)
        {
            LevelUp(); // 이 함수 안에서 스탯 증가가 일어남
        }

        // 2. 경험치 복구
        CurrentExp = savedData.currentExp;

        // 3. 체력/에너지는 풀로 채워주기 (마을 귀환 서비스)
        CurrentHp = MaxHp.Value;
        CurrentEnergy = 0; // 또는 MaxEnergy

        // UI 갱신
        OnLevelChanged?.Invoke((int)CurLevel);
        OnExpChanged?.Invoke(CurrentExp, MaxExp.Value);
    }

    // [추가] 현재 데이터 내보내기 (Save) - 레벨업 할 때나 던전 클리어 시 호출
    public CharacterRuntimeData ExportRuntimeData()
    {
        return new CharacterRuntimeData()
        {
            id = _data.id,
            level = (int)CurLevel,
            currentExp = CurrentExp
        };
    }
}
