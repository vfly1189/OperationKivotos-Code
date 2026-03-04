using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;

public class BossNormalAttack : BossSkillBase
{
    [SerializeField] private GameObject _lightningSubPrefab;
    [SerializeField] private AudioClip _sfx;

    public override void Cast(BossSkillContext context)
    {
        //StartCoroutine(ProcessSkillRoutine(context._targetPosition));
        ProcessSkillRoutineAsync(context._targetPosition).Forget();
    }

    private async UniTaskVoid ProcessSkillRoutineAsync(Vector3 targetPosition)
    {
        GameObject go = Managers.Resource.Instantiate(_lightningSubPrefab, targetPosition, Quaternion.identity);

        go.transform.position = targetPosition;
        go.GetComponent<ParticleSystem>().Play();

        // 오브젝트 파괴 시 자동 취소
        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(2.0f), cancellationToken: go.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (isCanceled) return;
        Managers.Resource.Destroy(go);
    }
}
