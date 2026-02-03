using UnityEngine;

// 데미지 정보를 담는 구조체 (확장성 확보)
public struct DamageInfo
{
    public float Amount;            // 데미지 양
    public GameObject Attacker;     // 공격자
    public Vector3 HitPoint;        // 타격 위치 (피격 이펙트 생성 위치)

    //public DamageType Type;      // 물리, 마법, 고정 데미지 등 추가 가능

    public DamageInfo(float amount, GameObject attacker, Vector3 hitPoint)
    {
        Amount = amount;
        Attacker = attacker;
        HitPoint = hitPoint;
    }
}

// 모든 "맞을 수 있는 오브젝트"는 이 인터페이스를 상속받음
public interface IDamageable
{
    void TakeDamage(DamageInfo damageInfo);
}