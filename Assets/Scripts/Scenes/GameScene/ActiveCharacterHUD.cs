using System.Security.Cryptography;
using UnityEngine;

public class ActiveCharacterHUD : MonoBehaviour
{
    [SerializeField] public SkillIconUI _qSkill;
    [SerializeField] public SkillIconUI _eSkill;
    [SerializeField] public StatUI _statUI;


    public void UnSubscribeEvent(BaseCharacter character)
    {
        if (character == null || character.Stat == null) return; // 안전장치 추가

        CharacterStat stat = character.Stat;

        stat.OnHpChanged -= _statUI.SetHp;
        stat.OnExpChanged -= _statUI.SetExp;
        stat.OnLevelChanged -= HandleLevelChanged; // 래퍼 함수 해제

        // 스킬 UI 이벤트 해제
        stat.OnEnergyChanged -= HandleActiveSkillEnergy;
        stat.OnUltimateStateChanged -= HandleActiveSkillReady;
    }

    public void SubscribeEvent(BaseCharacter character)
    {
        if (character == null || character.Stat == null) return; // 안전장치 추가

        CharacterStat stat = character.Stat;
        
        // (A) StatUI 초기화 & 구독
        _statUI.Initialize(stat);
        stat.OnHpChanged += _statUI.SetHp;
        stat.OnExpChanged += _statUI.SetExp;
        stat.OnLevelChanged += HandleLevelChanged;

        // (B) Skill UI (에너지 & 준비상태) 초기화 & 구독
        _qSkill.UpdateEnergy(stat.CurrentEnergy, stat.MaxEnergy.Value);

        // Ready 상태 초기화
        bool isReady = (stat.CurrentQSkillCoolTime <= 0) && (stat.CurrentEnergy >= stat.MaxEnergy.Value);
        _qSkill.SetUltimateReady(isReady);

        // 이벤트 연결
        stat.OnEnergyChanged += HandleActiveSkillEnergy;
        stat.OnUltimateStateChanged += HandleActiveSkillReady;
    }

    //정적인 데이터들 교체
    //Q스킬 아이콘, 에너지 채우는 색상, 에너지 꽉차면 빛나는 색상
    //E스킬 아이콘
    public void ChangeStaticData(CharacterDataSO data)
    {
        _qSkill.SetIcon(data.qSkillIcon);
        _qSkill.SetEnergyFillColor(data.energyFillColor);
        _qSkill.SetReadyGlowColor(data.ultimateGlowColor);
        _eSkill.SetIcon(data.eSkillIcon);
    }

    public void UpdateCooldowns(CharacterStat stat)
    {
        _qSkill.UpdateCooldown(stat.CurrentQSkillCoolTime, stat.QSkillCoolTime.Value);
        _eSkill.UpdateCooldown(stat.CurrentESkillCoolTime, stat.ESkillCoolTime.Value);
    }


    // --- 이벤트 핸들러 래퍼 (구독/해제를 명확히 하기 위함) ---
    private void HandleLevelChanged(int level) => _statUI.SetLevel(level);
    private void HandleActiveSkillEnergy(float cur, float max) => _qSkill.UpdateEnergy(cur, max);
    private void HandleActiveSkillReady(bool isReady) => _qSkill.SetUltimateReady(isReady);
}
