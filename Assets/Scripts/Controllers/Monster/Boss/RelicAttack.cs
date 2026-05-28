using System.Threading;
using Cysharp.Threading.Tasks;
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
        // 씬 전환/보스 사망 시 안전하게 멈추기 위해 CancellationToken 전달
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        // 비동기 함수 실행 (Fire and Forget)
        ProcessAttackAsync(context, token).Forget();
    }

    private async UniTaskVoid ProcessAttackAsync(BossSkillContext context, CancellationToken token)
    {
        _relicController = context._bossRelicController;

        // 색상 정보 가져오기
        bool isRed = _relicController.CurrentIsRed;

        // 타겟(경고) 위치 계산 (보스 전방 5m 지점을 중심점으로 설정)
        Vector3 warningZonePos = context._boss.transform.position + context._boss.transform.forward * 5.0f;
        warningZonePos.y = context._boss.transform.position.y; // 높이 고정

        // 소리 재생
        if (_sfx != null) Managers.Sound.Play(_sfx, Define.Sound.Effect);

        float safeZoneRadius = 2.5f; // 안전지대(빨간원) 여유 판정 2.5f

        // [빨강] 집중 공격 (위험 지대: 반지름 2인 원형 + 중앙)
        if (isRed)
        {
            // 1. 중앙에 1개 소환
            SpawnLightningRoutineAsync(warningZonePos, token).Forget();

            // 2. 반지름 2.0f의 원둘레를 따라 8개 촘촘히 소환
            int circleCount = 8;
            for (int i = 0; i < circleCount; i++)
            {
                float angle = i * Mathf.PI * 2f / circleCount;
                Vector3 spawnPos = warningZonePos + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2.0f;

                SpawnLightningRoutineAsync(spawnPos, token).Forget();

                // [추가] 번개가 생성되는 간격을 미세하게 벌려줌 (0.05초 = 50ms)
                // 이 줄이 추가되면 따다다다닥- 하고 원형으로 순차적으로 떨어집니다.
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.05f), cancellationToken: token).SuppressCancellationThrow();
            }
        }
        else
        {
            // [초록] 광역 공격 (_lightningPoints들 주변 무작위로 총 70개 소환)
            if (context._lightningPoints != null && context._lightningPoints.Length > 0)
            {
                int lightningCount = 70;
                float scatterRadius = 3.0f;

                for (int i = 0; i < lightningCount; i++)
                {
                    Vector3 randomPos = Vector3.zero;
                    bool validPosition = false;
                    int attempts = 0;

                    Transform baseSpot = context._lightningPoints[Random.Range(0, context._lightningPoints.Length)];

                    while (!validPosition && attempts < 10)
                    {
                        Vector2 randCircle = Random.insideUnitCircle * scatterRadius;
                        randomPos = baseSpot.position + new Vector3(randCircle.x, 0, randCircle.y);
                        randomPos.y = warningZonePos.y;

                        if (Vector3.Distance(randomPos, warningZonePos) > safeZoneRadius)
                        {
                            validPosition = true;
                        }
                        attempts++;
                    }

                    if (validPosition)
                    {
                        SpawnLightningRoutineAsync(randomPos, token).Forget();

                        // [추가] 무작위 위치에 떨어질 때도 0.02초 간격을 두어 시각적인 타격감을 줌
                        // 숫자를 Random으로 주면 더 불규칙하게 떨어집니다.
                        float randomDelay = Random.Range(0.01f, 0.02f);
                        await UniTask.Delay(System.TimeSpan.FromSeconds(randomDelay), cancellationToken: token).SuppressCancellationThrow();
                    }
                }
            }
        }

        // 렐릭 끄기
        _relicController.DeactivateRelic();

        // 비동기 함수 구조 유지를 위한 양보
        await UniTask.Yield(token);
    }

    // 개별 번개 생성 및 파괴 로직
    private async UniTaskVoid SpawnLightningRoutineAsync(Vector3 pos, CancellationToken token)
    {
        // 1. 생성
        GameObject go = Managers.Resource.Instantiate(_lightningPrefab, pos, Quaternion.identity);

        // 2. 파티클 재생
        if (go != null)
        {
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        // 3. 2초 대기 (토큰으로 캔슬 가능성 열어둠)
        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(2.0f), cancellationToken: token).SuppressCancellationThrow();

        // 만약 대기 도중 보스가 죽었거나 씬이 넘어가 취소되었다면 여기서 스탑
        if (isCanceled) return;

        // 4. 파괴
        if (go != null) Managers.Resource.Destroy(go);
    }
}