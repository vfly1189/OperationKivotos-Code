using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class ArisCharacter : BaseCharacter
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject _chargeEffect;
    [SerializeField] private Transform _chargePoint;
    [SerializeField] private float _fireDelay = 1.0f; // 발사 지연 시간 (Inspector에서 조절 가능)

    // 공격 신호가 오면(애니메이션 이벤트 등) 바로 발사하지 않고 대기 코루틴 시작
    protected override void PerformAttackAction()
    {
        // 1. 차지 이펙트 생성
        GameObject effectInstance = null;
        if (_chargeEffect != null && _chargePoint != null)
        {
            // 생성
            effectInstance = Managers.Resource.Instantiate(_chargeEffect, _chargePoint.position, _chargePoint.rotation);

            // [중요] 캐릭터가 회전해도 이펙트가 총구에 붙어있도록 부모 설정
            effectInstance.transform.SetParent(_chargePoint);
        }

        // 2. 코루틴에 이펙트 객체를 전달하여 나중에 끌 수 있게 함
        StartCoroutine(CoFireDelayed(_fireDelay, effectInstance));
    }

    // 파라미터로 생성된 이펙트(chargeVFX)를 받음
    private IEnumerator CoFireDelayed(float delay, GameObject chargeVFX)
    {
        // 1. 딜레이 대기 (차징 시간)
        yield return new WaitForSeconds(delay);

        // 2. [중요] 이펙트 삭제 (발사 직전)
        if (chargeVFX != null)
        {
            Managers.Resource.Destroy(chargeVFX);
        }

        // 3. 상태 체크 (캔슬되었는지 확인)
        if (_stateMachine.CurrentState != CharacterStateMachine.PlayerState.Attack && _stateMachine.CurrentState != CharacterStateMachine.PlayerState.Q_Skill && _stateMachine.CurrentState != CharacterStateMachine.PlayerState.E_Skill)
        {
            yield break;
        }

        // 4. 실제 발사
        FireOneBullet();
    }

    protected override void FireOneBullet()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

        GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);

        // 발사 시점의 회전값으로 갱신
        bulletObj.transform.position = _firePoint.position;
        bulletObj.transform.rotation = transform.rotation;

        ArisBulletController bulletScript = bulletObj.GetComponent<ArisBulletController>();
        if (bulletScript != null && Stat != null)
        {
            bulletScript.Init(Stat.Attack.Value, this.gameObject);
        }
    }
}
