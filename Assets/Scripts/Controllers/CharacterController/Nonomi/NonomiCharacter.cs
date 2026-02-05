using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class NonomiCharacter : BaseCharacter
{
    [Header("Rapid Fire Settings")]
    [SerializeField] private int _shotCount = 10;       // 한 번 공격에 나가는 총알 수
    [SerializeField] private float _fireDelay = 0.05f;  // 총알 사이 간격 (초)

    private Coroutine _rapidFireCoroutine; // 코루틴 참조 저장

    protected override void PerformAttackAction()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

        // [수정] 기존 코루틴이 있으면 중단
        if (_rapidFireCoroutine != null)
        {
            StopCoroutine(_rapidFireCoroutine);
        }

        _rapidFireCoroutine = StartCoroutine(CoRapidFire());
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
                bulletScript.Init(Stat.Attack.Value, this.gameObject);
            }
            PlayFireEffect();
            // 3. 다음 발사까지 대기
            yield return new WaitForSeconds(_fireDelay);
        }
    }

    // [추가] 비활성화 시 코루틴 정리
    protected override void OnDisable()
    {
        base.OnDisable();

        if (_rapidFireCoroutine != null)
        {
            StopCoroutine(_rapidFireCoroutine);
            _rapidFireCoroutine = null;
        }
    }
}
