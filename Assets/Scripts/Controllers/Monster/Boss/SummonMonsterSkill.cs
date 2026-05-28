using Cysharp.Threading.Tasks;
using System.Collections;
using System.Threading;
using UnityEngine;

public class SummonMonsterSkill : BossSkillBase
{
    [Header("설정")]
    [SerializeField] private GameObject _monsterRL;
    [SerializeField] private AudioClip _sfx;

    public override void Cast(BossSkillContext context)
    {
        // 프리팹이 활성화될 때 코루틴 시작
        ProcessSkill(context).Forget();
    }

    private async UniTask ProcessSkill(BossSkillContext context)
    {
        for(int i=0; i< context._spawnPoints.Length; i++)
        {
            GameObject monster = await MonsterFactory.CreateMonsterByAddressableKeyAsync(
                "Droid_Helmet_RL",
                Managers.Context.CurrentDungeonID,
                context._spawnPoints[i]
                );

            if (monster != null && context._bossController != null)
            {
                // [핵심] 보스에게 이 몬스터를 내가 소환했다고 명부에 등록시킴
                context._bossController.RegisterSummonedMonster(monster);
            }


        }
    }
}
