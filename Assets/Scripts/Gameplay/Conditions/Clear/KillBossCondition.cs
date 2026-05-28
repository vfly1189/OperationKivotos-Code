using UnityEngine;

public class KillBossCondition : BaseCondition
{
    private BossMonsterController _bossController;

    // mapRoot 대신, 필요한 BossMonsterController를 직접 받습니다. (의존성 주입)
    //public void Setup(BossMonsterController boss)
    //{
    //    _bossMonsterController = boss;

    //    if (_bossMonsterController == null)
    //    {
    //        Debug.LogError("[KillBossCondition] 전달된 보스 몬스터가 null입니다!");
    //        InvokeConditionMet();
    //        return;
    //    }

    //    _bossMonsterController.OnDead -= OnMonsterDead;
    //    _bossMonsterController.OnDead += OnMonsterDead;

    //    Debug.Log($"[KillBossCondition] 보스({_bossMonsterController.name}) 클리어 조건 등록 완료.");
    //}

    public override void SetUp()
    {
        if (_bossController == null)
        {
            Debug.LogError("[KillBossCondition] 전달된 보스 몬스터가 null입니다!");
            InvokeConditionMet();
            return;
        }

        _bossController.OnDead -= OnMonsterDead;
        _bossController.OnDead += OnMonsterDead;

        Debug.Log($"[KillBossCondition] 보스({_bossController.name}) 클리어 조건 등록 완료.");
    }

    public void SetBoss(GameObject boss) { _bossController = boss.GetComponent<BossMonsterController>(); }

    private void OnMonsterDead()
    {
        Debug.Log("몬스터사망 OnMonsterDead");
        InvokeConditionMet();
    }

    private void OnDestroy()
    {
        if (_bossController != null)
        {
            _bossController.OnDead -= OnMonsterDead;
        }
    }
}
