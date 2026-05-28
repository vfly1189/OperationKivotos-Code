using UnityEngine;


//강화재료를 등록했을때 설정할 경험치
public class ExpPreviewResult
{
    public int GainExp { get; }// 재료에서 얻는 총 경험치
                               // → "획득 경험치 +2950" 텍스트에 사용
    public int SimulatedLevel { get; } // 강화 후 예상 레벨
                                       // → 강화 레벨 텍스트에 사용 (+3 → +5)
    public int SimulatedExp { get; } // 레벨업 후 남는 잔여 경험치
                                     // → 경험치 바 현재값에 사용
    public int SimulatedRequireExp { get; } // 다음 레벨까지 필요 경험치
                                            // → 경험치 바 최대값에 사용
    

    public static readonly ExpPreviewResult Empty = new ExpPreviewResult(0, 0, 0, 1);

    public ExpPreviewResult(int gainExp, int simulatedLevel, int simulatedExp, int simulatedRequireExp)
    {
        GainExp = gainExp;
        SimulatedLevel = simulatedLevel;
        SimulatedExp = simulatedExp;
        SimulatedRequireExp = simulatedRequireExp;
    }
}
