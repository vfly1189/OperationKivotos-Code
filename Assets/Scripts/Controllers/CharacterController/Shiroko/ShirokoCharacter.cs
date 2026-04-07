using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class ShirokoCharacter : BaseCharacter
{
    public override void Init()
    {
        base.Init();
    }

    protected override void PerformAttackAction()
    {
        if (_bulletPrefab == null || _firePoint == null) return;

        FireOneBullet();
    }

  
}
