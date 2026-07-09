using Cysharp.Threading.Tasks;
using UnityEngine;

// 몬스터 사망 시 보상(경험치·크레딧·드랍) 지급 책임을 MonsterStat에서 분리한 컴포넌트.
// MonsterStat은 전투/스탯/죽음 감지만, 경제 로직은 여기서 처리한다. (SRP 분리 1단계)
// 프리팹 수정 없이 BaseMonsterController가 런타임에 부착·바인딩한다.
public class MonsterReward : MonoBehaviour
{
    private MonsterStat _stat;

    // BaseMonsterController가 호출: 컴포넌트 보장 + 스탯 바인딩 (풀 재사용·중복구독 안전)
    public static MonsterReward EnsureOn(GameObject go, MonsterStat stat)
    {
        if (go == null || stat == null) return null;

        var reward = go.GetComponent<MonsterReward>();
        if (reward == null) reward = go.AddComponent<MonsterReward>();
        reward.Bind(stat);
        return reward;
    }

    private void Bind(MonsterStat stat)
    {
        if (_stat == stat) return;
        if (_stat != null) _stat.OnKilledByAttacker -= HandleKilled;
        _stat = stat;
        _stat.OnKilledByAttacker += HandleKilled;
    }

    private void OnDestroy()
    {
        if (_stat != null) _stat.OnKilledByAttacker -= HandleKilled;
    }

    // 사망 순간(공격자 포함) 호출. 기존 MonsterStat.HandleDeath의 경제 블록을 그대로 이관.
    private void HandleKilled(GameObject attacker)
    {
        // 플레이어가 처치했을 때만 보상 지급
        if (attacker == null || !attacker.CompareTag("Player")) return;

        CharacterStat playerStat = attacker.GetComponent<CharacterStat>();
        if (playerStat == null) return;

        Managers.Party.AddExp(_stat.FinalExpReward);
        Managers.Wallet.AddCurrency(CurrencyType.Credit, _stat.FinalCreditReward);

        if (_stat.FinalExpReward > 0)
            UI_LootNotification.ShowGainExp(_stat.FinalExpReward).Forget();

        if (_stat.FinalCreditReward > 0)
            UI_LootNotification.ShowGainCredit(_stat.FinalCreditReward).Forget();

        if (_stat.DropTableID >= 0)
            Managers.Drop.RollAndGiveDropItems(_stat.DropTableID);
    }
}
