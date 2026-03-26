using UnityEngine;

public class KillBossCondition : BaseClearCondition
{
    private int _remainingMonsters;
    private BossMonsterController _bossMonsterController;

    public override void SetupCondition(GameObject mapRoot)
    {
        // 보스 찾기 (최상위나 특정 노드 밑에 있을 수 있으니 넓게 찾음)
        _bossMonsterController = mapRoot.GetComponentInChildren<BossMonsterController>(true);

        //방어 코드: 보스를 찾지 못했다면 에러 띄우고 즉시 클리어 처리 (진행 불가 방지)
        if (_bossMonsterController == null)
        {
            Debug.LogError("[KillBossCondition] 맵에서 보스 몬스터를 찾을 수 없습니다! 맵에 보스가 스폰되었는지 확인하세요.");
            InvokeConditionMet(); // 무한 대기 방지용
            return;
        }

        // 기존 구독 해제 후 재구독 (안전장치)
        _bossMonsterController.OnDead -= OnMonsterDead;
        _bossMonsterController.OnDead += OnMonsterDead;

        Debug.Log($"[KillBossCondition] 보스({_bossMonsterController.name}) 클리어 조건 등록 완료.");
    }

    private void OnMonsterDead()
    {       
        InvokeConditionMet();
    }

    // [추가] 오브젝트 파괴 시 구독 해제 (메모리 누수 방지)
    private void OnDestroy()
    {
        if (_bossMonsterController != null)
        {
            _bossMonsterController.OnDead -= OnMonsterDead;
        }
    }
}
