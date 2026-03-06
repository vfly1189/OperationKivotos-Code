using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

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
                Debug.Log($"강화 성공! Lv.{prevLevel} → Lv.{prevLevel + 1}");
                break;
            case WeaponUpgradeService.UpgradeResult.Fail:
                Debug.Log($"강화 실패... Lv.{prevLevel} 유지");
                break;
            case WeaponUpgradeService.UpgradeResult.NotEnoughCurrency:
                Debug.Log("재화가 부족합니다.");
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
        List<BaseCharacter> characters = Managers.Party.GetMemeber();

        for (int i = 0; i < characters.Count && i < _partyButtons.Length; i++)
        {
            _partyButtons[i].image.sprite = await Managers.Resource.LoadAsync<Sprite>(characters[i].Stat.GetPortrait());
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
        RefreshRateInfo(targetLevel); // 테이블 키 정책에 따라 weaponLevel로 바꿔도 됨
    }

    private void RefreshCurrencyInfo(WeaponEnhanceCost cost)
    {
        int haveGold = Managers.Wallet.GetCurrency(CurrencyType.Credit);

        // “재료 3개” UI가 아직 1개만 있으니 우선 Material1을 강화석으로 표시
        //int haveStone = Managers.Wallet.GetItemCount(cost.Material1ID); // 없으면 인벤토리 매니저 함수로 교체하세요
        int haveStone = 100;

        _requireCreditText.text = cost.RequireGold.ToString("N0");
        _requrieStoneText.text = cost.Material1Count.ToString("N0");
        _havingCreditText.text = haveGold.ToString("N0");
        _havingStoneText.text = haveStone.ToString("N0");

        _requireCreditText.color = haveGold >= cost.RequireGold ? Color.white : Color.red;
        _requrieStoneText.color = haveStone >= cost.Material1Count ? Color.white : Color.red;

        bool canUpgrade = haveGold >= cost.RequireGold && haveStone >= cost.Material1Count;
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
        _requrieStoneText.text = "-";
        _havingCreditText.text = "-";
        _havingStoneText.text = "-";
        _enhancementRateText.text = "-";
        _enhancementRateText.color = Color.white;
        _upgradeButton.interactable = false;
    }

    private BaseCharacter GetSelectedCharacter()
    {
        int currentIndex = Managers.Party.GetCurrentCharacterIndex();
        return Managers.Party.GetMemeber()[currentIndex];
    }
}

