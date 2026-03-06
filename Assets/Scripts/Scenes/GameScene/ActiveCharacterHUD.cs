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
        stat.OnExpChanged -= _statUI.SetExp;
        stat.OnLevelChanged -= HandleLevelChanged;

        stat.OnEnergyChanged -= HandleActiveSkillEnergy;
        stat.OnUltimateStateChanged -= HandleActiveSkillReady;
    }

    public void SubscribeEvent(BaseCharacter character)
    {
        if (character == null || character.Stat == null) return;

        CharacterStat stat = character.Stat;

        _statUI.Initialize(stat);
        stat.OnHpChanged += _statUI.SetHp;
        stat.OnExpChanged += _statUI.SetExp;
        stat.OnLevelChanged += HandleLevelChanged;

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

        _qSkill.SetEnergyFillColor(charData.energyFillColor);
        _qSkill.SetReadyGlowColor(charData.ultimateGlowColor);

        if (charData.qSkillIcon != null && charData.qSkillIcon.RuntimeKeyIsValid())
        {
            Sprite qIcon = await Managers.Resource.LoadAsync<Sprite>(charData.qSkillIcon);
            _qSkill.SetIcon(qIcon);
        }

        if (charData.eSkillIcon != null && charData.eSkillIcon.RuntimeKeyIsValid())
        {
            Sprite eIcon = await Managers.Resource.LoadAsync<Sprite>(charData.eSkillIcon);
            _eSkill.SetIcon(eIcon);
        }
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
