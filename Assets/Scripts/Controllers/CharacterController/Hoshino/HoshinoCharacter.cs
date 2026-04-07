using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class HoshinoCharacter : BaseCharacter
{
    [Header("Shotgun Settings")]
    [SerializeField] private int _pelletCount = 5;     // 한 번에 나가는 총알 개수
    [SerializeField] private float _spreadAngle = 20f; // 탄 퍼짐 각도

    [Header("ESkill Fire Point")]
    [SerializeField] private Transform _eSkillFirePoint;
    [SerializeField] private ParticleSystem _eSkillFireEffect;

    public override void Init()
    {
        base.Init();
    }

    // 공격 상태일 때 매 프레임 실행될 로직
    protected override void PerformAttackAction()
    { 
        if (_bulletPrefab == null || _firePoint == null) return;

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
                bulletScript.Init(CalculatedDamage(), this.gameObject);
            }
        }
        PlayFireEffect();
    }

    protected override void OnESkillEvent(AudioClip sfx) 
    {
        // 1. 풀링으로 총알 생성 (위치/회전은 총구 기준)
        GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _eSkillFirePoint.position, _eSkillFirePoint.rotation);

        bulletObj.transform.position = _eSkillFirePoint.position;
        // 캐릭터가 바라보는 방향 기준으로 회전
        bulletObj.transform.rotation = transform.rotation;
        // 2. 데미지 주입
        BulletController bulletScript = bulletObj.GetComponent<BulletController>();
        if (bulletScript != null && Stat != null)
        {
            bulletScript.Init(CalculatedDamage(), this.gameObject);
        }
        PlayESkillFireEffect();
    }

    protected virtual void PlayESkillFireEffect()
    {
        if (_eSkillFireEffect == null) return;
        _eSkillFireEffect.Stop();
        _eSkillFireEffect.Play();
    }
}
