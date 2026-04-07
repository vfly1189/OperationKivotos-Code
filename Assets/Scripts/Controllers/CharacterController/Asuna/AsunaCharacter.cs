using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class AsunaCharacter : BaseCharacter
{

    public override void Init()
    {
        base.Init();
    }

    // 공격 상태일 때 매 프레임 실행될 로직
    protected override void PerformAttackAction()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

        FireOneBullet();
    }


}
