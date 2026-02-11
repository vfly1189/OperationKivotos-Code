using UnityEngine;

public class Lightning : MonoBehaviour
{
    [SerializeField] private float _damage = 10f;

    private void OnTriggerEnter(Collider other)
    {
        //// 플레이어 태그 확인
        //if (other.CompareTag("Player"))
        //{
        //    // 플레이어의 IDamageable 인터페이스나 Stat 컴포넌트 가져오기
        //    var stat = other.GetComponent<CharacterStat>(); // 혹은 IDamageable
        //    if (stat != null)
        //    {
        //        // 데미지 공식 적용 (방어력 등은 TakeDamage 내부에서 처리)
        //        DamageInfo info = new DamageInfo(DamageType.Normal, _damage, this.gameObject);
        //        stat.TakeDamage(info);
        //    }

        //    // 단발성 공격이면 충돌체 끄기 (중복 데미지 방지)
        //    if (!_isDotDamage)
        //    {
        //        GetComponent<Collider>().enabled = false;
        //    }
        //}
        if (other.TryGetComponent<IDamageable>(out IDamageable target))
        {
            // 정확한 타격 위치 계산 (이펙트 용)
            Vector3 hitPoint = other.ClosestPoint(transform.position);

            Debug.Log("번개 데미지 실행");
            // 인터페이스 메서드 호출 (상대가 Player든 Monster든 상관 안 함)
            target.TakeDamage(new DamageInfo(_damage, this.gameObject, hitPoint));
        }
    }
}
