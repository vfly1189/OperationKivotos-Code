using UnityEngine;

public class WeaponUpgradeService
{
    public enum UpgradeResult
    {
        Success,
        Fail,
        NotEnoughCurrency,
        AlreadyMaxLevel
    }

    public UpgradeResult TryUpgrade(BaseCharacter target)
    {
        //int currentLevel = target.Stat.WeaponLevel;

        //// 1. 만렙 체크
        //if (currentLevel >= 5) return UpgradeResult.AlreadyMaxLevel;

        //// 2. 다음 레벨 비용 데이터 가져오기
        //string key = $"{target.Stat._weaponData.itemID}_{currentLevel + 1}";

        ////if (!Managers.Data.WeaponDict.TryGetValue(key, out WeaponData nextData))
        ////    return UpgradeResult.AlreadyMaxLevel;

        //WeaponData nextData = Managers.Data.GetData<string, WeaponData>(key);
        //if (nextData == null)
        //    return UpgradeResult.AlreadyMaxLevel;

        //// 3. 재화 소모 시도
        //bool consumed = Managers.Wallet.TryConsumeMultiple(
        //    CurrencyType.Credit, nextData.CostGold,
        //    CurrencyType.EnhanceStone, nextData.CostStones
        //);
        //if (!consumed) return UpgradeResult.NotEnoughCurrency;

        //EnhancementRateData rateData = Managers.Data.GetData<int, EnhancementRateData>(currentLevel);
        //// 4. 확률 판정
        //if (rateData != null)
        //{
        //    if (UnityEngine.Random.value <= rateData.successRate)
        //    {
        //        target.Stat.WeaponLevelUp();
        //        return UpgradeResult.Success;
        //    }
        //}

        //return UpgradeResult.Fail;
        return UpgradeResult.Fail;
    }
}