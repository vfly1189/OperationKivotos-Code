using UnityEngine;

public class MonsterStat : BaseStat
{
    // 몬스터 전용: 처치 시 주는 경험치, 드랍 아이템 확률 등
    public float DropExpAmount = 10f;

    public void Init(MonsterDataSO data, int stageLevel)
    {
        base.Init();
        // 스테이지 레벨에 따른 스탯 뻥튀기 적용
        float multiplier = 1 + (stageLevel * 0.1f);

        MaxHp.SetBaseValue(data.MaxHp * multiplier);
        Attack.SetBaseValue(data.Attack * multiplier);

        CurrentHp = MaxHp.Value;
    }
}
