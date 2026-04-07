using Cysharp.Threading.Tasks;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class HinaCharacter : BaseCharacter
{
    [Header("Rapid Fire Settings")]
    [SerializeField] private int _shotCount = 8;       // 한 번 공격에 나가는 총알 수
    [SerializeField] private float _fireDelay = 0.05f;  // 총알 사이 간격 (초)
    public override void Init()
    {
        base.Init();
    }

    protected override void PerformAttackAction()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

       

        RapidFireAsync(_actionCts.Token).Forget();
    }

   
    // [핵심 2] 코루틴을 비동기 메서드로 변경
    private async UniTaskVoid RapidFireAsync(CancellationToken token)
    {
        for (int i = 0; i < _shotCount; i++)
        {
            // 발사 중간에 사망하거나 다른 스킬을 쓰면 연사 즉시 중단!
            if (token.IsCancellationRequested) return;

            // 1. 풀링으로 총알 생성
            GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);
            bulletObj.transform.position = _firePoint.position;
            bulletObj.transform.rotation = transform.rotation;

            // 2. 데미지 주입
            BulletController bulletScript = bulletObj.GetComponent<BulletController>();
            if (bulletScript != null && Stat != null)
            {
                bulletScript.Init(CalculatedDamage(), this.gameObject);
            }
            PlayFireEffect();

            // 3. 다음 발사까지 대기 (UniTask.Delay 활용, 가비지 제로)
            // SuppressCancellationThrow로 토큰 캔슬 시 에러 없이 종료
            bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(_fireDelay), cancellationToken: token).SuppressCancellationThrow();

            if (isCanceled) return; // 취소되었다면 루프 탈출
        }
    }
}
