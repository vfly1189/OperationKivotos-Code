using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class SerikaCharacter : BaseCharacter
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject _bulletPrefab; // 풀링용 프리팹 (Poolable 필수)
    [SerializeField] private Transform _firePoint;     // 총구 위치

    [Header("Rapid Fire Settings")]
    [SerializeField] private int _shotCount = 1;       // 한 번 공격에 나가는 총알 수
    [SerializeField] private float _fireDelay = 0.1f;  // 총알 사이 간격 (초)
    public override void Init()
    {
        base.Init();
    }

    // 공격 상태일 때 매 프레임 실행될 로직
    protected override void PerformAttackAction()
    {
        Debug.Log("세리카 공격 시작");

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
    }

    protected override void OnCutsceneEnded(PlayableDirector director)
    {
        Debug.Log("세리카 전용 컷신 종료 -> 일반상태로 전환");
        ChangeState(PlayerState.Idle);
    }

    protected override void PlaySFXOnly()
    {
        Managers.Sound.Play(_sfx, Define.Sound.Effect, 1.866f);
    }
}
