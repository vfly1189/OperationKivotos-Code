using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class ShirokoCharacter : BaseCharacter
{
    [Header("Rapid Fire Settings")]
    [SerializeField] private int _shotCount = 1;       // 한 번 공격에 나가는 총알 수
    [SerializeField] private float _fireDelay = 0.1f;  // 총알 사이 간격 (초)
    public override void Init()
    {
        base.Init();
    }

    protected override void PerformAttackAction()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

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
