using UnityEngine;

public class Lightning : MonoBehaviour
{
    [SerializeField] private float _damage = 10f;


    private void OnTriggerEnter(Collider other)
    {

        if (other.TryGetComponent<IDamageable>(out IDamageable target))
        {
            // 정확한 타격 위치 계산 (이펙트 용)
            Vector3 hitPoint = other.ClosestPoint(transform.position);

            DamageInfo damageInfo = new DamageInfo();
            damageInfo.Amount = _damage;
            damageInfo.Attacker = this.gameObject;
            damageInfo.HitPoint = hitPoint;
            damageInfo.IsCritical = false;

            GameLog.Log("번개 데미지 실행");
            // 인터페이스 메서드 호출 (상대가 Player든 Monster든 상관 안 함)
            target.TakeDamage(damageInfo);
        }
    }
}
