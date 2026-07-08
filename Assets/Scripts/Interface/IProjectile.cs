using UnityEngine;

// 발사체 공용 계약. SpawnProjectiles가 구체 타입(BulletController/ArisBulletController 등)을
// 몰라도 데미지 패킷을 주입할 수 있게 한다.
public interface IProjectile
{
    void Init(DamageInfo damage, GameObject shooter);
}
