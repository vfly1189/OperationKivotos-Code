using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneCanvas : MonoBehaviour
{
    [SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결

    [Header("Data Source")]
    [SerializeField] private SchoolDataSO[] _schoolDatas;

    [Header("파티 슬롯")]
    [SerializeField] private PartySlotUI[] _partySlots;

    [Header("Skill UI")]
    [SerializeField] private SkillIconUI _qSkill;
    [SerializeField] private SkillIconUI _eSkill;

    [Header("Stat UI")]
    [SerializeField] private StatUI _statUI;

    private PartyManager _partyManager;
    private BaseCharacter _cachedActiveCharacter; // 현재 UI가 구독 중인 캐릭터
    int _schoolIdx;

    public void SetPartyManager(PartyManager partyManager) => _partyManager = partyManager;

    void Start()
    {
        _schoolIdx = Managers.Context.SchoolIdx;

        // 1. UI 기본 정보 세팅 (이름, 초상화)
        InitPartySlotsInfo();

        if (_partyManager != null)
        {
            // 2. 파티 슬롯 이벤트 영구 연결 (HP, 궁극기 상태)
            ConnectPermanentPartyEvents();

            // 3. 캐릭터 교체 이벤트 연결
            _partyManager.OnCharacterChanged += UpdateActiveCharacterUI;

            // 4. 초기 캐릭터 UI 세팅
            UpdateActiveCharacterUI(0);
        }
    }

    void Update()
    {
        // [최적화] 매 프레임 도는 것은 오직 '쿨타임' 뿐
        UpdateActiveSkillCooldowns();

       
    }

    // ========================================================================
    // [1] 파티 슬롯 (우측) - 한 번 연결하면 끝 (캐릭터가 파티에서 빠지지 않는 한)
    // ========================================================================
    void InitPartySlotsInfo()
    {
        for (int i = 0; i < 4; i++)
        {
            if (i >= _partySlots.Length) break;
            CharacterDataSO charData = _schoolDatas[_schoolIdx].characters[i];

            _partySlots[i].SetCharacterName(charData.nameKR);
            _partySlots[i].SetPortrait(Managers.Context.SelectedSchool.characters[i].Portrait);
        }
    }

    void ConnectPermanentPartyEvents()
    {
        List<BaseCharacter> members = _partyManager.PartyMembers;

        for (int i = 0; i < members.Count; i++)
        {
            if (i >= _partySlots.Length) break;

            BaseCharacter character = members[i];
            int slotIndex = i; // Closure 캡처용

            // (A) HP 변경 -> 파티 슬롯 HP바 갱신
            character.Stat.OnHpChanged += (cur, max) =>
                _partySlots[slotIndex].UpdateHP(cur, max);

            // (B) 궁극기 상태 변경 -> 파티 슬롯 'Ready' 이펙트 토글
            character.Stat.OnUltimateStateChanged += (isReady) =>
                _partySlots[slotIndex].SetUltimateReady(isReady);

            // (C) 초기 상태 동기화
            _partySlots[slotIndex].UpdateHP(character.Stat.CurrentHp, character.Stat.MaxHp.Value);

            // CharacterStat의 로직으로 계산된 준비 상태 가져오기 (쿨타임0 & 에너지Full)
            bool isReady = (character.Stat.CurrentQSkillCoolTime <= 0) &&
                           (character.Stat.CurrentEnergy >= character.Stat.MaxEnergy.Value);
            _partySlots[slotIndex].SetUltimateReady(isReady);
        }
    }

    // ========================================================================
    // [2] 활성 캐릭터 (메인 UI) - 교체될 때마다 갈아끼우기
    // ========================================================================
    void UpdateActiveCharacterUI(int charIndex)
    {
        // --- 1. 기존 캐릭터 구독 해제 ---
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            CharacterStat oldStat = _cachedActiveCharacter.Stat;
            oldStat.OnHpChanged -= _statUI.SetHp;
            oldStat.OnExpChanged -= _statUI.SetExp;
            oldStat.OnLevelChanged -= HandleLevelChanged; // 래퍼 함수 해제

            // 스킬 UI 이벤트 해제
            oldStat.OnEnergyChanged -= HandleActiveSkillEnergy;
            oldStat.OnUltimateStateChanged -= HandleActiveSkillReady;
        }

        // --- 2. 새 캐릭터 가져오기 ---
        BaseCharacter newChar = _partyManager.PartyMembers[charIndex];
        CharacterDataSO charData = Managers.Context.SelectedSchool.characters[charIndex];
        _cachedActiveCharacter = newChar;

        // 아이콘 등 정적 데이터 교체
        if (charData != null)
        {
            _qSkill.SetIcon(charData.qSkillIcon);
            _qSkill.SetEnergyFillColor(charData.energyFillColor);
            _qSkill.SetReadyGlowColor(charData.ultimateGlowColor);
            _eSkill.SetIcon(charData.eSkillIcon);
        }

        // --- 3. 새 캐릭터 구독 및 초기화 ---
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            CharacterStat newStat = _cachedActiveCharacter.Stat;

            // (A) StatUI 초기화 & 구독
            _statUI.Initialize(newStat);
            newStat.OnHpChanged += _statUI.SetHp;
            newStat.OnExpChanged += _statUI.SetExp;
            newStat.OnLevelChanged += HandleLevelChanged;

            // (B) Skill UI (에너지 & 준비상태) 초기화 & 구독
            _qSkill.UpdateEnergy(newStat.CurrentEnergy, newStat.MaxEnergy.Value);

            // Ready 상태 초기화
            bool isReady = (newStat.CurrentQSkillCoolTime <= 0) && (newStat.CurrentEnergy >= newStat.MaxEnergy.Value);
            _qSkill.SetUltimateReady(isReady);

            // 이벤트 연결
            newStat.OnEnergyChanged += HandleActiveSkillEnergy;
            newStat.OnUltimateStateChanged += HandleActiveSkillReady;
        }
    }

    // --- 이벤트 핸들러 래퍼 (구독/해제를 명확히 하기 위함) ---
    private void HandleLevelChanged(int level) => _statUI.SetLevel(level);
    private void HandleActiveSkillEnergy(float cur, float max) => _qSkill.UpdateEnergy(cur, max);
    private void HandleActiveSkillReady(bool isReady) => _qSkill.SetUltimateReady(isReady);


    // ========================================================================
    // [3] 매 프레임 업데이트 (Update)
    // ========================================================================
    void UpdateActiveSkillCooldowns()
    {
        if (_cachedActiveCharacter == null) return;
        CharacterStat stat = _cachedActiveCharacter.Stat;
        if (stat == null) return;

        // Q 스킬 쿨타임 (텍스트, 링)
        _qSkill.UpdateCooldown(stat.CurrentQSkillCoolTime, stat.QSkillCoolTime.Value);

        // E 스킬 쿨타임 (텍스트, 링)
        _eSkill.UpdateCooldown(stat.CurrentESkillCoolTime, stat.ESkillCoolTime.Value);
    }

    void OnDestroy()
    {
        if (_partyManager != null)
            _partyManager.OnCharacterChanged -= UpdateActiveCharacterUI;

        // 마지막 캐릭터 구독 해제
        if (_cachedActiveCharacter != null)
        {
            // ... 해제 로직 (UpdateActiveCharacterUI의 해제 부분과 동일) ...
        }
    }
}
