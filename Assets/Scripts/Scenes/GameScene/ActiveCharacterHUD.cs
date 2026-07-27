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

        stat.HealthComp.OnHpChanged -= _statUI.SetHp;
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
        stat.HealthComp.OnHpChanged += _statUI.SetHp;
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

        GameLog.Log($"[ActiveHUD] ChangeStaticDataAsync 호출: {charData.nameKey}");

        _qSkill.SetEnergyFillColor(charData.energyFillColor);
        _qSkill.SetReadyGlowColor(charData.ultimateGlowColor);


        // [Phase 3d] 씬 전환 중 이 HUD가 파괴되면 로드도 취소 — 토큰이 없으면 인플라이트 로드가
        //  다음 씬의 스코프에 얹혀 아무도 안 쓰는 아틀라스로 남는다.
        var token = this.GetCancellationTokenOnDestroy();

        // 규칙: "캐릭터ID_Q_Icon"
        //  [R5 후속] 스킬 아이콘 아틀라스는 '파티'의 수명 — 파티가 GameScene↔던전을 넘나들어도 유지된다.
        //  Scene 스코프면 전환마다 언로드→재로드(churn)라, Party 스코프로 재배치해 재사용한다.
        string qIconName = $"{charData.nameKey}_Q_Icon";
        Sprite qIcon = await Managers.Resource.GetSpriteFromAtlasAsync("SkillIconAtlas", qIconName, ResourceScopeType.Party, token);
        if (qIcon != null) _qSkill.SetIcon(qIcon);

        // 규칙: "캐릭터ID_E_Icon"
        string eIconName = $"{charData.nameKey}_E_Icon";
        Sprite eIcon = await Managers.Resource.GetSpriteFromAtlasAsync("SkillIconAtlas", eIconName, ResourceScopeType.Party, token);
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
