using Cysharp.Threading.Tasks;
using UnityEngine;

public class ActiveCharacterHUD : MonoBehaviour
{
    [SerializeField] public SkillIconUI _qSkill;
    [SerializeField] public SkillIconUI _eSkill;
    [SerializeField] public StatUI _statUI;

    public void UnSubscribeEvent(BaseCharacter character)
    {
        if (character == null || character.Stat == null) return;

        CharacterStat stat = character.Stat;

        stat.OnHpChanged -= _statUI.SetHp;
        //stat.OnExpChanged -= _statUI.SetExp;
        //stat.OnLevelChanged -= HandleLevelChanged;

        Managers.Party.OnPartyExpChanged    -= _statUI.SetExp;
        Managers.Party.OnPartyLevelChanged  -= HandleLevelChanged;

        stat.OnEnergyChanged -= HandleActiveSkillEnergy;
        stat.OnUltimateStateChanged -= HandleActiveSkillReady;
    }

    public void SubscribeEvent(BaseCharacter character)
    {
        if (character == null || character.Stat == null) return;

        CharacterStat stat = character.Stat;

        _statUI.Initialize(stat);
        stat.OnHpChanged += _statUI.SetHp;
        Managers.Party.OnPartyExpChanged   += _statUI.SetExp;
        Managers.Party.OnPartyLevelChanged += HandleLevelChanged;

        _qSkill.UpdateEnergy(stat.CurrentEnergy, stat.MaxEnergy.Value);
        bool isReady = (stat.CurrentQSkillCoolTime <= 0) && (stat.CurrentEnergy >= stat.MaxEnergy.Value);
        _qSkill.SetUltimateReady(isReady);

        stat.OnEnergyChanged += HandleActiveSkillEnergy;
        stat.OnUltimateStateChanged += HandleActiveSkillReady;
    }

    // 비동기 스킬 아이콘 갱신 함수
    public async UniTask ChangeStaticDataAsync(CharacterDataSO charData)
    {
        if (charData == null) return;

        Debug.Log($"[ActiveHUD] ChangeStaticDataAsync 호출: {charData.nameKey}");

        _qSkill.SetEnergyFillColor(charData.energyFillColor);
        _qSkill.SetReadyGlowColor(charData.ultimateGlowColor);


        // 규칙: "캐릭터ID_Q_Icon"
        string qIconName = $"{charData.nameKey}_Q_Icon";
        Sprite qIcon = await Managers.Resource.GetSpriteFromAtlasAsync("SkillIconAtlas", qIconName);      
        if (qIcon != null) _qSkill.SetIcon(qIcon);

        // 규칙: "캐릭터ID_E_Icon"
        string eIconName = $"{charData.nameKey}_E_Icon";
        Sprite eIcon = await Managers.Resource.GetSpriteFromAtlasAsync("SkillIconAtlas", eIconName);
        if (eIcon != null) _eSkill.SetIcon(eIcon);
    }

    public void UpdateCooldowns(CharacterStat stat)
    {
        _qSkill.UpdateCooldown(stat.CurrentQSkillCoolTime, stat.QSkillCoolTime.Value);
        _eSkill.UpdateCooldown(stat.CurrentESkillCoolTime, stat.ESkillCoolTime.Value);
    }

    // 래퍼 함수들
    private void HandleLevelChanged(int level) => _statUI.SetLevel(level);
    private void HandleActiveSkillEnergy(float cur, float max) => _qSkill.UpdateEnergy(cur, max);
    private void HandleActiveSkillReady(bool isReady) => _qSkill.SetUltimateReady(isReady);
}
