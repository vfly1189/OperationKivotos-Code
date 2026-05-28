using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;

public class GenesisSkill : BossSkillBase
{
    [Header("설정")]
    [SerializeField] private GameObject _genesisEffect;
    [SerializeField] private AudioClip _sfx;

    public override void Cast(BossSkillContext context)
    {
        // 프리팹이 활성화될 때 코루틴 시작
        ProcessSkillRoutineAsync(context._targetPosition).Forget();
    }

    private async UniTask ProcessSkillRoutineAsync(Vector3 centerPos)
    {
        //GameObject go = Object.Instantiate(_genesisEffect, centerPos, Quaternion.identity);
        GameObject go = Managers.Resource.Instantiate(_genesisEffect, centerPos, Quaternion.identity);
        go.transform.position = centerPos;
        go.GetComponent<ParticleSystem>().Play();

        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(5.0f), cancellationToken: go.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (isCanceled) return;
        Managers.Destroy(go);
    }
}
