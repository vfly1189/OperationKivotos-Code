using System.Collections;
using UnityEngine;

public class RelicAttack : BossSkillBase
{
    [Header("프리팹")]
    [SerializeField] private GameObject _lightningPrefab; // 번개 이펙트
    [SerializeField] private AudioClip _sfx;

    private BossRelicController _relicController;

    // 타임라인 두 번째 시그널에서 호출
    public override void Cast(BossSkillContext context)
    {
        // 코루틴 시작을 위해 context의 보스(MonoBehaviour)나 자기 자신을 사용
        StartCoroutine(ProcessAttack(context));
    }

    private IEnumerator ProcessAttack(BossSkillContext context)
    {
        _relicController = context._bossRelicController;

        // 색상 정보 가져오기
        bool isRed = _relicController.CurrentIsRed;

        // 타겟 위치 계산 (보스 전방 5m)
        Vector3 warningZonePos = context._boss.transform.position + context._boss.transform.forward * 5.0f;

        // 소리 재생
        if (_sfx != null) Managers.Sound.Play(_sfx, Define.Sound.Effect);

        // 공격 실행
        if (isRed)
        {
            // [빨강] 집중 공격 (장판 위치 1곳)
            StartCoroutine(SpawnLightningRoutine(warningZonePos));
        }
        else
        {
            // [초록] 광역 공격 (전체 포인트)
            if (context._lightningPoints != null)
            {
                foreach (Transform spot in context._lightningPoints)
                {
                    // 장판과 거리가 먼 곳만 생성 (안전지대 로직 주석 풀어서 활용 가능)
                    if (Vector3.Distance(spot.position, warningZonePos) > 3.0f)
                    {
                        StartCoroutine(SpawnLightningRoutine(spot.position));
                    }
                }
            }
        }

        // 렐릭 끄기
        _relicController.DeactivateRelic();

        yield return null;
    }

    // 개별 번개 생성 및 파괴 로직
    private IEnumerator SpawnLightningRoutine(Vector3 pos)
    {
        //생성
        GameObject go = Managers.Resource.Instantiate(_lightningPrefab, pos, Quaternion.identity);

        //파티클 재생
        if (go != null)
        {
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        //2초 대기
        yield return new WaitForSeconds(2.0f);

        //파괴
        if (go != null) Managers.Resource.Destroy(go);
    }
}
