using UnityEngine;

public class WeaponUpgradeService
{
    const int MAX_WEAPON_LEVEL = 25;

    public enum UpgradeResult
    {
        Success, Fail, NotEnoughCurrency, AlreadyMaxLevel
    }

    public UpgradeResult TryUpgrade(BaseCharacter target)
    {
        int currentLevel = target.Stat.WeaponLevel;
        if (currentLevel >= MAX_WEAPON_LEVEL) return UpgradeResult.AlreadyMaxLevel;

        int targetLevel = currentLevel + 1;

        // 1. 데이터 로드
        EnhancementRateData rateData = Managers.Data.GetData<int, EnhancementRateData>(currentLevel);
        WeaponEnhanceCost costData = Managers.Data.GetData<int, WeaponEnhanceCost>(targetLevel);

        if (rateData == null || costData == null)
            return UpgradeResult.AlreadyMaxLevel;

        // 2. 재화 소모 시도
        if (!TryConsume(costData))
            return UpgradeResult.NotEnoughCurrency;

        // 3. 확률 판정
        if (UnityEngine.Random.value <= rateData.successRate)
        {
            target.Stat.WeaponLevelUp();
            return UpgradeResult.Success;
        }

        return UpgradeResult.Fail;
    }

    private bool TryConsume(WeaponEnhanceCost cost)
    {
        int reqGold = cost.RequireGold;
        int[] reqMats = { cost.Material1Count, cost.Material2Count, cost.Material3Count };
        int[] matIDs = { (int)UpgradeStone_ID.Common, (int)UpgradeStone_ID.Uncommon, (int)UpgradeStone_ID.Rare };

        // 1. 보유량 확인 (부족하면 즉시 false)
        if (Managers.Wallet.GetCurrency(CurrencyType.Credit) < reqGold) return false;

        for (int i = 0; i < 3; i++)
        {
            if (reqMats[i] > 0 && Managers.Inventory.GetItemCount(ItemCategory.Material, matIDs[i]) < reqMats[i])
                return false;
        }

        // 2. 실제 재화 소모
        Managers.Wallet.ConsumeCurrency(CurrencyType.Credit, reqGold);

        for (int i = 0; i < 3; i++)
        {
            if (reqMats[i] > 0)
                Managers.Inventory.ConsumeMaterial(matIDs[i], reqMats[i]);
        }

        return true;
    }

    public int GetMaxLevel(BaseCharacter target)
    => target.Stat.WeaponData?.levelStats?.Length ?? MAX_WEAPON_LEVEL;
}