using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public enum UpgradeStone_ID
{
    Common = 30000,
    Uncommon,
    Rare
}


public class UI_UpgradePopUp : UI_PopUp
{
    [Header("Button")]
    [SerializeField] public Button[] _partyButtons;
    [SerializeField] public Button _upgradeButton;

    [Header("레벨 텍스트")]
    [SerializeField] public TextMeshProUGUI _currentWeaponLevelText;
    [SerializeField] public TextMeshProUGUI _nextWeaponLevelText;

    [Header("강화 비용 텍스트")]
    [SerializeField] public TextMeshProUGUI _requireCreditText;
    [SerializeField] public TextMeshProUGUI[] _requrieStoneTexts;

    [Header("보유 재화 텍스트")]
    [SerializeField] public TextMeshProUGUI _havingCreditText;
    [SerializeField] public TextMeshProUGUI[] _havingStoneTexts;

    [Header("강화 확률 텍스트")]
    [SerializeField] public TextMeshProUGUI _enhancementRateText;

    [Header("무기 이미지")]
    [SerializeField] public Image _weaponImage;

    private WeaponUpgradeService _upgradeService;

    public override void Init()
    {
        base.Init();

        _upgradeService = new WeaponUpgradeService();

        RegisterPartyButtons();
        RegisterUpgradeButton();

        RefreshUIAsync().Forget();
    }

    private void RegisterPartyButtons()
    {
        for (int i = 0; i < _partyButtons.Length; i++)
        {
            int capturedIndex = i;
            _partyButtons[i].onClick.RemoveAllListeners();
            _partyButtons[i].onClick.AddListener(() => OnClickPartyButton(capturedIndex));
        }
    }

    private void RegisterUpgradeButton()
    {
        _upgradeButton.onClick.RemoveAllListeners();
        _upgradeButton.onClick.AddListener(OnClickUpgradeButton);
    }

    private void OnClickPartyButton(int index)
    {
        Managers.Party.TrySwap(index);
        RefreshUIAsync().Forget();
    }

    private void OnClickUpgradeButton()
    {
        BaseCharacter selected = GetSelectedCharacter();
        int prevLevel = selected.Stat.WeaponLevel;

        var result = _upgradeService.TryUpgrade(selected);

        HandleUpgradeResult(result, prevLevel);
        RefreshUIAsync().Forget();
    }

    private void HandleUpgradeResult(WeaponUpgradeService.UpgradeResult result, int prevLevel)
    {
        switch (result)
        {
            case WeaponUpgradeService.UpgradeResult.Success:
                GameLog.Log($"강화 성공! Lv.{prevLevel} → Lv.{prevLevel + 1}");
                break;
            case WeaponUpgradeService.UpgradeResult.Fail:
                GameLog.Log($"강화 실패... Lv.{prevLevel} 유지");
                break;
            case WeaponUpgradeService.UpgradeResult.NotEnoughCurrency:
                GameLog.Log("재화가 부족합니다.");
                break;
        }
    }

    private async UniTaskVoid RefreshUIAsync()
    {
        await RefreshPartyButtons();
        await RefreshWeaponInfoAsync();
    }

    private async UniTask RefreshPartyButtons()
    {
        int currentIndex = Managers.Party.GetCurrentCharacterIndex();
        List<BaseCharacter> characters = Managers.Party.GetMember();

        for (int i = 0; i < characters.Count && i < _partyButtons.Length; i++)
        {
            //_partyButtons[i].image.sprite = await Managers.Resource.LoadAsync<Sprite>(characters[i].Stat.GetPortrait());

            string emblemKey = "Emblem_Icon_Favor_" + characters[i].Stat.GetNameKey();
            
            _partyButtons[i].image.sprite = await Managers.Resource.GetSpriteFromAtlasAsync("CharacterEmblemsAtlas", emblemKey);
            _partyButtons[i].interactable = (i != currentIndex);
        }
    }

    private async UniTask RefreshWeaponInfoAsync()
    {
        BaseCharacter selected = GetSelectedCharacter();
        var weaponData = selected.Stat.WeaponData;

        if (weaponData == null)
        {
            SetMaxLevelUI();
            _currentWeaponLevelText.text = "무기 데이터 없음";
            _nextWeaponLevelText.text = "-";
            return;
        }

        int weaponLevel = selected.Stat.WeaponLevel;
        int maxWeaponLevel = weaponData.levelStats != null && weaponData.levelStats.Length > 0
            ? weaponData.levelStats.Length
            : 25;

        bool isMaxLevel = weaponLevel >= maxWeaponLevel;

        // 아이콘 로드 (AssetReferenceSprite)
        if (_weaponImage != null)
        {
            Sprite icon = null;

            if (weaponData.weaponIcon != null)
                icon = await Managers.Resource.LoadAsync<Sprite>(weaponData.weaponIcon);

            _weaponImage.sprite = icon;
            _weaponImage.enabled = (icon != null);
        }

        _currentWeaponLevelText.text = isMaxLevel
            ? "현재 레벨 : MAX"
            : $"현재 레벨 : Lv. {weaponLevel}";
        _nextWeaponLevelText.text = isMaxLevel ? "-" : $"다음 레벨 : Lv. {weaponLevel + 1}";

        if (isMaxLevel)
        {
            SetMaxLevelUI();
            return;
        }

        int targetLevel = weaponLevel + 1;

        // 비용 테이블에서 다음 레벨 비용 가져오기
        WeaponEnhanceCost cost = Managers.Data.GetData<int, WeaponEnhanceCost>(targetLevel);
        if (cost == null)
        {
            SetMaxLevelUI();
            return;
        }

        RefreshCurrencyInfo(cost);
        RefreshRateInfo(weaponLevel); // 테이블 키 정책에 따라 weaponLevel로 바꿔도 됨
    }

    private void RefreshCurrencyInfo(WeaponEnhanceCost cost)
    {
        // 1. 골드 UI 갱신
        int haveGold = Managers.Wallet.GetCurrency(CurrencyType.Credit);
        _requireCreditText.text = cost.RequireGold.ToString("N0");
        _havingCreditText.text = haveGold.ToString("N0");
        _requireCreditText.color = haveGold >= cost.RequireGold ? Color.white : Color.red;

        bool canUpgrade = haveGold >= cost.RequireGold;

        // 2. 재료 UI 갱신 (배열 활용)
        int[] reqMats = { cost.Material1Count, cost.Material2Count, cost.Material3Count };
        int[] matIDs = { (int)UpgradeStone_ID.Common, (int)UpgradeStone_ID.Uncommon, (int)UpgradeStone_ID.Rare };

        for (int i = 0; i < 3; i++)
        {
            int reqCount = reqMats[i];
            int haveCount = Managers.Inventory.GetItemCount(ItemCategory.Material, matIDs[i]);

            _requrieStoneTexts[i].text = reqCount.ToString("N0");
            _havingStoneTexts[i].text = haveCount.ToString("N0");
            _requrieStoneTexts[i].color = haveCount >= reqCount ? Color.white : Color.red;

            // 하나라도 부족하면 강화 불가 처리
            if (haveCount < reqCount)
                canUpgrade = false;
        }

        // 3. 버튼 활성화 상태 적용
        _upgradeButton.interactable = canUpgrade;
    }

    private void RefreshRateInfo(int targetLevel)
    {
        EnhancementRateData rateData = Managers.Data.GetData<int, EnhancementRateData>(targetLevel);
        if (rateData == null)
        {
            _enhancementRateText.text = "성공 확률: -";
            _enhancementRateText.color = Color.white;
            return;
        }

        float successPercent = rateData.successRate * 100f;
        _enhancementRateText.text = $"성공 확률: {successPercent:0.#}%";
        _enhancementRateText.color = successPercent >= 80 ? Color.white
                                   : successPercent >= 50 ? Color.yellow
                                   : Color.red;
    }

    private void SetMaxLevelUI()
    {
        _requireCreditText.text = "-";

        _requrieStoneTexts[0].text = "-";
        _requrieStoneTexts[1].text = "-";
        _requrieStoneTexts[2].text = "-";

        _havingCreditText.text = "-";

        _havingStoneTexts[0].text = "-";
        _havingStoneTexts[1].text = "-";
        _havingStoneTexts[2].text = "-";

        _enhancementRateText.text = "-";
        _enhancementRateText.color = Color.white;
        _upgradeButton.interactable = false;
    }

    private BaseCharacter GetSelectedCharacter()
    {
        int currentIndex = Managers.Party.GetCurrentCharacterIndex();
        return Managers.Party.GetMember()[currentIndex];
    }
}

