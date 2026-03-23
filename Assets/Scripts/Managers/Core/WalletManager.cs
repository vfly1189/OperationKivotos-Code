using System;
using System.Collections.Generic;
using System.Configuration;
using UnityEngine;

public enum CurrencyType
{
    Credit        
}

public class WalletManager
{
    // 실제 재화가 저장되는 딕셔너리
    private Dictionary<CurrencyType, int> _currencies = new Dictionary<CurrencyType, int>();

    // 재화가 변동될 때 UI 등을 업데이트하기 위한 이벤트
    public event Action<CurrencyType, int> OnCurrencyChanged;

    public void Init()
    {
        // 초기화 시 모든 재화를 0으로 세팅 (또는 세이브 파일에서 로드)
        foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
        {
            _currencies[type] = 0;
        }

        // TODO: Managers.Data.Load() 같은 곳에서 저장된 재화 불러오기
        // 임시로 테스트용 지급
        AddCurrency(CurrencyType.Credit, 50000000);
    }

    // 재화 확인
    public int GetCurrency(CurrencyType type)
    {
        return _currencies.TryGetValue(type, out int amount) ? amount : 0;
    }

    // 재화 획득 (던전 클리어, 퀘스트 보상 등)
    public void AddCurrency(CurrencyType type, int amount)
    {
        if (amount < 0) return;

        _currencies[type] += amount;
        Debug.Log($"[Currency] 획득: {type} +{amount} (현재: {_currencies[type]})");

        OnCurrencyChanged?.Invoke(type, _currencies[type]);
    }

    // 재화 소모 (강화, 상점 구매 등)
    // 리턴값이 true면 소모 성공, false면 잔액 부족으로 실패
    public bool ConsumeCurrency(CurrencyType type, int amount)
    {
        if (amount < 0) return false;

        if (GetCurrency(type) >= amount)
        {
            _currencies[type] -= amount;
            Debug.Log($"[Currency] 소모: {type} -{amount} (현재: {_currencies[type]})");

            OnCurrencyChanged?.Invoke(type, _currencies[type]);
            return true;
        }

        Debug.LogWarning($"[Currency] 잔액 부족: {type} (필요: {amount}, 현재: {GetCurrency(type)})");
        return false; // 잔액 부족
    }

    // 다중 재화 동시 소모 체크 (강화할 때 골드+강화석 둘 다 필요한 경우)
    public bool CanConsumeMultiple(CurrencyType type1, int amount1, CurrencyType type2, int amount2)
    {
        return GetCurrency(type1) >= amount1 && GetCurrency(type2) >= amount2;
    }

    // 다중 재화 동시 소모 실행
    public bool TryConsumeMultiple(CurrencyType type1, int amount1, CurrencyType type2, int amount2)
    {
        if (CanConsumeMultiple(type1, amount1, type2, amount2))
        {
            ConsumeCurrency(type1, amount1);
            ConsumeCurrency(type2, amount2);
            return true;
        }
        return false;
    }

    public bool CanConsumeCurreny(CurrencyType type, int amount)
    {
        if (_currencies[type] >= amount) return true;
        else return false;
    }

    public WalletSaveData GetSaveData()
    {
        WalletSaveData save = new WalletSaveData();

        foreach (KeyValuePair<CurrencyType, int> entry in _currencies)
        {
            save.currencyData.Add(new WalletDataEntry { currencyType = entry.Key, amount = entry.Value });
        }

        return save;
    }

    public void LoadSaveData(WalletSaveData save)
    {
        if (save?.currencyData == null) return;

        foreach (WalletDataEntry entry in save.currencyData)
        {
            _currencies[entry.currencyType] = entry.amount;
            OnCurrencyChanged?.Invoke(entry.currencyType, entry.amount); // ← 추가
        }
    }
}
