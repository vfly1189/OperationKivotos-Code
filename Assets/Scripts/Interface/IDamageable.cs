using UnityEngine;

public struct DamageInfo
{
    public float Amount;            // 데미지 양
    public GameObject Attacker;     // 공격자
    public Vector3 HitPoint;        // 타격 위치 (피격 이펙트 생성 위치)
    public bool IsCritical; // 치명타 여부를 데미지를 줄 때 미리 계산해서 넘김
                            //public DamageType Type;      // 물리, 마법, 고정 데미지 등 추가 가능

    public DamageInfo(float amount, GameObject attacker, bool isCritical = false)
    {
        Amount = amount;
        Attacker = attacker;
        HitPoint = Vector3.zero; // -> 충돌할때 채워넣을거임
        IsCritical = isCritical;
    }
}

// 모든 "맞을 수 있는 오브젝트"는 이 인터페이스를 상속받음
public interface IDamageable
{
    void TakeDamage(DamageInfo damageInfo);
}