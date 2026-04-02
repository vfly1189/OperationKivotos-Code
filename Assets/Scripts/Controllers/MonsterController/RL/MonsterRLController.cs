using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class MonsterRLController : RangedMonsterController
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject _bulletPrefab; // 풀링용 프리팹 (Poolable 필수)
    [SerializeField] private Transform _firePoint;     // 총구 위치
    [SerializeField] private ParticleSystem _rocketFireEffect;

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
            bulletScript.Init(Stat.Attack.Value, this.gameObject);
        }
        PlayFireEffect();
    }

    private void PlayFireEffect()
    {
        if (_rocketFireEffect == null) return;

        _rocketFireEffect.Stop();
        _rocketFireEffect.Play();
    }
}
