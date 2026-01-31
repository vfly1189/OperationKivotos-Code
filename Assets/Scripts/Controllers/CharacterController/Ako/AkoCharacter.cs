using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class AkoCharacter : BaseCharacter
{


    public override void Init()
    {
        base.Init();
    }

    // 공격 상태일 때 매 프레임 실행될 로직
    protected override void PerformAttackAction()
    {
        if(_bulletPrefab == null || _firePoint == null) return;

        FireOneBullet();
    }
    private void FireOneBullet()
    {
        // 1. 총알 생성 (풀링)
        GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);

        // 2. 위치/회전 보정
        bulletObj.transform.position = _firePoint.position;
        bulletObj.transform.rotation = transform.rotation; // 캐릭터 정면 방향

        // 3. 데미지 주입
        BulletController bulletScript = bulletObj.GetComponent<BulletController>();
        if (bulletScript != null && Stat != null)
        {
            bulletScript.Init(Stat.Attack.Value);
        }
        PlayFireEffect();
    }

}
