using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;

public class RelicActivation : BossSkillBase
{
    [Header("설정")]
    private BossRelicController _relicController;
    [SerializeField] private GameObject _yellowWarning;

    private bool _currentIsRed;
    public override void Cast(BossSkillContext context) 
    {
        // 1. 렐릭 컨트롤러 찾기 (최초 1회만 해도 됨)
        if (_relicController == null)
        {
            _relicController = context._boss.GetComponent<BossRelicController>();
        }

        if (_relicController != null)
        {
            // 2. 렐릭 활성화 및 회전 시작! (색상 정보 받아옴)
            _currentIsRed = _relicController.ActivateRandomRelic();
        }

        //StartCoroutine(ProcessSkillRoutine(context._boss));
        ProcessSkillRoutineAsync(context._boss).Forget();
    }


    private async UniTaskVoid ProcessSkillRoutineAsync(GameObject boss)
    {
        Vector3 skillPos = boss.transform.position + boss.transform.forward * 5.0f;
        GameObject warningZone = Managers.Resource.Instantiate(_yellowWarning, skillPos, Quaternion.identity);
        warningZone.GetComponent<ParticleSystem>().Play();

        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(3.0f), cancellationToken: warningZone.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (isCanceled) return;

        Managers.Resource.Destroy(warningZone);
    }
}
