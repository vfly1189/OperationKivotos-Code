using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class KarinCharacter : BaseCharacter
{

    public override void Init()
    {
        base.Init();
    }

    // 공격 상태일 때 매 프레임 실행될 로직
    protected override void PerformAttackAction()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

        FireOneBullet();
    }

    private void FireOneBullet()
    {
        // 1. 풀링으로 총알 생성 (위치/회전은 총구 기준)
        GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);

        bulletObj.transform.position = _firePoint.position;
        // 캐릭터가 바라보는 방향 기준으로 회전
        bulletObj.transform.rotation = transform.rotation;
        // 2. 데미지 주입
        BulletController bulletScript = bulletObj.GetComponent<BulletController>();
        if (bulletScript != null && Stat != null)
        {
            bulletScript.Init(Stat.Attack.Value);
        }
        PlayFireEffect();
    }

}
