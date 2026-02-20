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

    public override void Init()
    {
        base.Init();

        // 루프에서 인덱스를 캡처해서 버튼마다 리스너 등록
        for (int i = 0; i < _partyButtons.Length; i++)
        {
            int capturedIndex = i; // 클로저 캡처 문제 방지 (중요!)

            _partyButtons[i].onClick.RemoveAllListeners();
            _partyButtons[i].onClick.AddListener(() =>
            {
                OnClickPartyButton(capturedIndex);
            });
        }

        // [추가] 강화 버튼 리스너 등록
        _upgradeButton.onClick.RemoveAllListeners();
        _upgradeButton.onClick.AddListener(OnClickUpgradeButton);

        RefreshUI();
    }


    // 버튼 눌렸을 때 실행되는 함수
    private void OnClickPartyButton(int index)
    {
        // [추가] 선택된 캐릭터 인덱스를 파티 매니저에 알려줌
        Managers.Party.TrySwap(index);

        // UI 전체 갱신
        RefreshUI();
    }


    //패널 한번 Refresh
    public void RefreshUI()
    {
        int currentIndex = Managers.Party.GetCurrentCharacterIndex();
        //왼쪽 프로필 4개
        List<BaseCharacter> characters = Managers.Party.GetMemeber();

        for(int i=0; i<characters.Count; i++)
        {
            BaseCharacter character = characters[i];
            _partyButtons[i].image.sprite = character.Stat.GetData().Portrait;

            _partyButtons[i].interactable = (i != currentIndex);         
        }

        // 현재 선택된 캐릭터의 무기 정보
        BaseCharacter selected = characters[currentIndex];
        int weaponLevel = selected.Stat.WeaponLevel;

        // 무기 이미지
        _weaponImage.sprite = selected.Stat._weaponData.icon;

        // 레벨 텍스트
        _currentWeaponLevelText.text = $"현재 레벨 : Lv. {weaponLevel}";
        _nextWeaponLevelText.text = $"다음 레벨 : Lv. {weaponLevel + 1}";

        // DataManager에서 다음 레벨 비용 가져오기
        string key = $"{selected.Stat._weaponData.itemID}_{weaponLevel + 1}";
        bool isMaxLevel = weaponLevel >= 5;

        if (!isMaxLevel && Managers.Data.WeaponDict.TryGetValue(key, out WeaponData nextData))
        {
            // 필요 재화 텍스트
            _requireCreditText.text = nextData.CostGold.ToString("N0");
            _requrieStoneText.text = nextData.CostStones.ToString("N0");

            // 보유 재화 텍스트
            int haveGold = Managers.Wallet.GetCurrency(CurrencyType.Credit);
            int haveStone = Managers.Wallet.GetCurrency(CurrencyType.EnhanceStone);
            _havingCreditText.text = haveGold.ToString("N0");
            _havingStoneText.text = haveStone.ToString("N0");

            // 재화 부족하면 빨간색으로 표시
            _requireCreditText.color = haveGold >= nextData.CostGold ? Color.white : Color.red;
            _requrieStoneText.color = haveStone >= nextData.CostStones ? Color.white : Color.red;

            // [추가] 강화 확률 텍스트
            if (Managers.Data.EnhanceRateDict.TryGetValue(weaponLevel, out EnhancementRateData rateData))
            {
                //int successPercent = Mathf.RoundToInt(rateData.successRate * 100);
                float successPercent = rateData.successRate * 100;
                _enhancementRateText.text = $"성공 확률: {successPercent}%";

                // 확률이 낮을수록 빨간색으로 경고
                _enhancementRateText.color = successPercent >= 80 ? Color.white
                                           : successPercent >= 50 ? Color.yellow
                                           : Color.red;
            }
            else
            {
                _enhancementRateText.text = "성공 확률: -";
                _enhancementRateText.color = Color.white;
            }

            // 버튼 활성화 여부
            bool canUpgrade = haveGold >= nextData.CostGold && haveStone >= nextData.CostStones;
            _upgradeButton.interactable = canUpgrade;
        }
        else
        {
            // 최대 레벨 처리
            _currentWeaponLevelText.text = "현재 레벨 : MAX";
            _nextWeaponLevelText.text = "-";
            _requireCreditText.text = "-";
            _requrieStoneText.text = "-";
            _enhancementRateText.text = "-"; // [추가]
            _enhancementRateText.color = Color.white; // [추가]
            _upgradeButton.interactable = false;
        }
    }

    private void OnClickUpgradeButton()
    {
        int currentIndex = Managers.Party.GetCurrentCharacterIndex();
        BaseCharacter selected = Managers.Party.GetMemeber()[currentIndex];
        int weaponLevel = selected.Stat.WeaponLevel;

        string key = $"{selected.Stat._weaponData.itemID}_{weaponLevel + 1}";

        // 1. 다음 레벨 비용 데이터 가져오기
        if (!Managers.Data.WeaponDict.TryGetValue(key, out WeaponData nextData)) return;

        // 2. 재화 소모 시도 (실패 시 함수 종료)
        bool consumed = Managers.Wallet.TryConsumeMultiple(
            CurrencyType.Credit, nextData.CostGold,
            CurrencyType.EnhanceStone, nextData.CostStones
        );
        if (!consumed) return;

        // 3. 확률 판정
        if (Managers.Data.EnhanceRateDict.TryGetValue(weaponLevel, out EnhancementRateData rateData))
        {
            float roll = UnityEngine.Random.value; // 0.0 ~ 1.0

            if (roll <= rateData.successRate)
            {
                // 강화 성공!
                //selected.Stat.WeaponLevel = (weaponLevel + 1);
                selected.Stat.WeaponLevelUp();
                Debug.Log($"강화 성공! Lv.{weaponLevel} → Lv.{weaponLevel + 1}");
                // TODO: 성공 이펙트 연출
            }
            else
            {
                // 강화 실패 (재화는 이미 소모됨)
                Debug.Log($"강화 실패... Lv.{weaponLevel} 유지");
                // TODO: 실패 이펙트 연출
            }
        }

        // 4. UI 갱신
        RefreshUI();
    }
}
