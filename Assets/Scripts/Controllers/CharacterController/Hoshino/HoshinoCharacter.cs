using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class HoshinoCharacter : BaseCharacter
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject _bulletPrefab; // 총알 프리팹 (Trail Renderer 포함된 것)
    [SerializeField] private Transform _firePoint;     // 총구 위치 (빈 오브젝트)

    [Header("Shotgun Settings")]
    [SerializeField] private int _pelletCount = 1;     // 한 번에 나가는 총알 개수
    [SerializeField] private float _spreadAngle = 15f; // 탄 퍼짐 각도

    public override void Init()
    {
        base.Init();
    }

    // 공격 상태일 때 매 프레임 실행될 로직
    protected override void PerformAttackAction()
    {
        //// 토키만의 공격 로직: 총알 생성
        //if (_bulletPrefab != null && _firePoint != null)
        //{
        //    Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);
        //    // 사운드 재생 등...
        //}
        //Debug.Log("토키: 빵야!");


        if (_bulletPrefab == null || _firePoint == null) return;

        // 사운드 재생 (공용 함수 활용)
        // Managers.Sound.Play(_shotSfx); 

        // 샷건 발사 (부채꼴)
        // _pelletCount가 5라면, -30도 ~ +30도 사이로 5발 발사

        // 시작 각도 계산 (왼쪽 끝)
        float startAngle = -_spreadAngle / 2f;
        // 각 총알 사이의 간격
        float angleStep = _spreadAngle / (_pelletCount - 1);

        for (int i = 0; i < _pelletCount; i++)
        {
            float currentAngle = startAngle + (angleStep * i);

            // 캐릭터가 바라보는 방향 기준으로 회전
            Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
            Quaternion finalRotation = _firePoint.rotation * rotation;

            // 총알 생성
            GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _firePoint.position, finalRotation);

            // 총알 초기화 (데미지는 Stat에서 가져옴)
            BulletController bulletScript = bulletObj.GetComponent<BulletController>();
            if (bulletScript != null && Stat != null)
            {
                bulletScript.Init(Stat.Attack.Value);
            }
        }

        Debug.Log($"호시노: 샷건 발사! ({_pelletCount}발)");
    }

    
}
