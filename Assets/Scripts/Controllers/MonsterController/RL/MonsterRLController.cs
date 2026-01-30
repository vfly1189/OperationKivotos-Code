using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class MonsterRLController : MonsterController
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject _bulletPrefab; // 풀링용 프리팹 (Poolable 필수)
    [SerializeField] private GameObject _fireEffect;
    [SerializeField] private Transform _firePoint;     // 총구 위치

    [Header("Rapid Fire Settings")]
    [SerializeField] private int _shotCount = 1;       // 한 번 공격에 나가는 총알 수
    [SerializeField] private float _fireDelay = 0.05f;  // 총알 사이 간격 (초)

    protected override void PerformAttackAction()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

        StartCoroutine(CoRapidFire());
    }

    private IEnumerator CoRapidFire()
    {
        Vector3 position = _firePoint.position;


        for (int i = 0; i < _shotCount; i++)
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
            // 3. 다음 발사까지 대기
            yield return new WaitForSeconds(_fireDelay);
        }
    }

    private void PlayFireEffect()
    {
        if (_fireEffect == null || _firePoint == null) return;

        // 이펙트 생성 (총구 위치, 총구 회전)
        // 이펙트가 총구에 붙어서 따라다니길 원하면 parent를 _firePoint로 설정
        GameObject effect = Managers.Resource.Instantiate(_fireEffect, _firePoint.position, _firePoint.rotation);
        effect.transform.SetParent(_firePoint);

        // (선택) 총구에 붙이기: effect.transform.SetParent(_firePoint); 

        // 파티클 시스템이면 재생 확인 (보통 Play On Awake가 켜져 있어서 자동 재생됨)
        var ps = effect.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            Debug.Log("파티클 재생");
            ps.Play();
        }

        // 이펙트는 보통 1~2초 뒤 자동 삭제되도록 프리팹 자체에 로직이 있거나 여기서 예약
        //Managers.Resource.Destroy(effect);
    }
}
