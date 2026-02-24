using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] public TextMeshProUGUI _requrieStoneText;

    [Header("보유 재화 텍스트")]
    [SerializeField] public TextMeshProUGUI _havingCreditText;
    [SerializeField] public TextMeshProUGUI _havingStoneText;

    [Header("강화 확률 텍스트")]
    [SerializeField] public TextMeshProUGUI _enhancementRateText;

    [Header("무기 이미지")]
    [SerializeField] public Image _weaponImage;

    // 장비강화 로직은 서비스에게 위임
    private WeaponUpgradeService _upgradeService;

    public override void Init()
    {
        base.Init();

        _upgradeService = new WeaponUpgradeService();

        RegisterPartyButtons();
        RegisterUpgradeButton();

        RefreshUI();
    }

    // ==================== 버튼 리스너 등록 ====================

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

    // ==================== 버튼 클릭 핸들러 ====================

    private void OnClickPartyButton(int index)
    {
        Managers.Party.TrySwap(index);
        RefreshUI();
    }

    private void OnClickUpgradeButton()
    {
        BaseCharacter selected = GetSelectedCharacter();
        WeaponUpgradeService.UpgradeResult result = _upgradeService.TryUpgrade(selected);

        HandleUpgradeResult(result, selected.Stat.WeaponLevel);
        RefreshUI();
    }

    private void HandleUpgradeResult(WeaponUpgradeService.UpgradeResult result, int prevLevel)
    {
        switch (result)
        {
            case WeaponUpgradeService.UpgradeResult.Success:
                Debug.Log($"강화 성공! Lv.{prevLevel} → Lv.{prevLevel + 1}");
                // TODO: 성공 이펙트
                break;
            case WeaponUpgradeService.UpgradeResult.Fail:
                Debug.Log($"강화 실패... Lv.{prevLevel} 유지");
                // TODO: 실패 이펙트
                break;
            case WeaponUpgradeService.UpgradeResult.NotEnoughCurrency:
                Debug.Log("재화가 부족합니다.");
                // TODO: 부족 팝업
                break;
        }
    }

    // ==================== UI 갱신 ====================

    public void RefreshUI()
    {
        RefreshPartyButtons();
        RefreshWeaponInfo();
    }

    private void RefreshPartyButtons()
    {
        int currentIndex = Managers.Party.GetCurrentCharacterIndex();
        List<BaseCharacter> characters = Managers.Party.GetMemeber();

        for (int i = 0; i < characters.Count; i++)
        {
            _partyButtons[i].image.sprite = characters[i].Stat.GetData().Portrait;
            _partyButtons[i].interactable = (i != currentIndex);
        }
    }

    private void RefreshWeaponInfo()
    {
        BaseCharacter selected = GetSelectedCharacter();
        int weaponLevel = selected.Stat.WeaponLevel;
        bool isMaxLevel = weaponLevel >= 5;

        _weaponImage.sprite = selected.Stat._weaponData.icon;
        _currentWeaponLevelText.text = isMaxLevel
            ? "현재 레벨 : MAX"
            : $"현재 레벨 : Lv. {weaponLevel}";
        _nextWeaponLevelText.text = isMaxLevel ? "-" : $"다음 레벨 : Lv. {weaponLevel + 1}";

        if (isMaxLevel)
        {
            SetMaxLevelUI();
            return;
        }

        string key = $"{selected.Stat._weaponData.itemID}_{weaponLevel + 1}";
        if (!Managers.Data.WeaponDict.TryGetValue(key, out WeaponData nextData))
        {
            SetMaxLevelUI();
            return;
        }

        RefreshCurrencyInfo(nextData);
        RefreshRateInfo(weaponLevel);
    }

    private void RefreshCurrencyInfo(WeaponData nextData)
    {
        int haveGold = Managers.Wallet.GetCurrency(CurrencyType.Credit);
        int haveStone = Managers.Wallet.GetCurrency(CurrencyType.EnhanceStone);

        _requireCreditText.text = nextData.CostGold.ToString("N0");
        _requrieStoneText.text = nextData.CostStones.ToString("N0");
        _havingCreditText.text = haveGold.ToString("N0");
        _havingStoneText.text = haveStone.ToString("N0");

        _requireCreditText.color = haveGold >= nextData.CostGold ? Color.white : Color.red;
        _requrieStoneText.color = haveStone >= nextData.CostStones ? Color.white : Color.red;

        bool canUpgrade = haveGold >= nextData.CostGold && haveStone >= nextData.CostStones;
        _upgradeButton.interactable = canUpgrade;
    }

    private void RefreshRateInfo(int weaponLevel)
    {
        if (!Managers.Data.EnhanceRateDict.TryGetValue(weaponLevel, out EnhancementRateData rateData))
        {
            _enhancementRateText.text = "성공 확률: -";
            _enhancementRateText.color = Color.white;
            return;
        }

        float successPercent = rateData.successRate * 100;
        _enhancementRateText.text = $"성공 확률: {successPercent}%";
        _enhancementRateText.color = successPercent >= 80 ? Color.white
                                   : successPercent >= 50 ? Color.yellow
                                   : Color.red;
    }

    private void SetMaxLevelUI()
    {
        _requireCreditText.text = "-";
        _requrieStoneText.text = "-";
        _enhancementRateText.text = "-";
        _enhancementRateText.color = Color.white;
        _upgradeButton.interactable = false;
    }

    
    // ==================== 헬퍼 ====================
    private BaseCharacter GetSelectedCharacter()
    {
        int currentIndex = Managers.Party.GetCurrentCharacterIndex();
        return Managers.Party.GetMemeber()[currentIndex];
    }
}
