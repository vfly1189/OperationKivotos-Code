using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;

public class ConvergingLightningSkill : BossSkillBase
{
    [Header("설정")]
    [SerializeField] private GameObject _lightningSubPrefab;
    [SerializeField] private float _startDistance = 8f; // 시작 거리
    [SerializeField] private int _stepCount = 5;        // 몇 번에 걸쳐 다가갈지 (예: 5번 꽝꽝꽝꽝꽝)
    [SerializeField] private float _interval = 0.2f;    // 한 번 치고 대기하는 시간
    [SerializeField] private AudioClip _sfx;

    public override void Cast(BossSkillContext context)
    {
        // 프리팹이 활성화될 때 코루틴 시작
        //StartCoroutine(ProcessSkillRoutine(context._targetPosition));

        ProcessSkillRoutineAsync(context._targetPosition, context._boss.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid ProcessSkillRoutineAsync(Vector3 centerPos, System.Threading.CancellationToken token)
    {
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        GameObject[] instances = new GameObject[4];
        ParticleSystem[] particles = new ParticleSystem[4];

        for (int i = 0; i < 4; i++)
        {
            instances[i] = Managers.Resource.Instantiate(_lightningSubPrefab, centerPos, Quaternion.identity);
            particles[i] = instances[i].GetComponent<ParticleSystem>();
        }

        for (int step = 0; step < _stepCount; step++)
        {
            float t = (float)step / (_stepCount - 1);
            float currentDist = Mathf.Lerp(_startDistance, 0f, t);

            for (int i = 0; i < 4; i++)
            {
                if (instances[i] != null)
                {
                    instances[i].transform.position = centerPos + (directions[i] * currentDist);
                    particles[i].Clear();
                    particles[i].time = 0;
                    particles[i].Play();
                }
            }
            Managers.Sound.Play(_sfx, Define.Sound.Effect);

            // 취소되면 즉시 루프 탈출
            bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(_interval), cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled) break;
        }

        // 루프 종료 후 1초 대기 (취소 시 무시하고 삭제 단계로 넘어감)
        await UniTask.Delay(System.TimeSpan.FromSeconds(1.0f), cancellationToken: token).SuppressCancellationThrow();

        for (int i = 0; i < 4; i++)
        {
            if (instances[i] != null) Managers.Resource.Destroy(instances[i]);
        }
    }
}
