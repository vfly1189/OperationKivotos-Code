using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnhancementRateData
{
    // JSON의 키 이름과 일치해야 합니다.
    public int currentLevel;          // 현재 무기 레벨 (1 -> 2로 갈 때의 데이터는 Level: 1)
    public float successRate;  // 성공 확률 (0.0 ~ 1.0)
    public float failRate;
}

[System.Serializable] // <<<<< 여기도 확인!
public class WeaponEnhanceMentDataLoader : ILoader<int, EnhancementRateData>
{
    // JSON의 최상위 배열 이름("rates" 등)과 동일해야 합니다.
    public List<EnhancementRateData> rates = new List<EnhancementRateData>();

    public Dictionary<int, EnhancementRateData> MakeDict()
    {
        Dictionary<int, EnhancementRateData> dict = new Dictionary<int, EnhancementRateData>();

        foreach (EnhancementRateData rateData in rates)
        {
            // Level을 Key로 사용
            if (!dict.ContainsKey(rateData.currentLevel))
            {
                dict.Add(rateData.currentLevel, rateData);
            }
        }
        return dict;
    }
}
