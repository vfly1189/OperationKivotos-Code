using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class IoriCharacter : BaseCharacter
{

    public override void Init()
    {
        base.Init();
    }

    // 공격 상태일 때 매 프레임 실행될 로직
    protected override void PerformAttackAction()
    {
        //// 토키만의 공격 로직: 총알 생성
        //if (_bulletPrefab != null && _firePoint != null)
        //{
        //    Instantiate(_bulletPrefab, _firePoint.position, _firePoint.rotation);
        //    // 사운드 재생 등...
        //}
        //Debug.Log("토키: 빵야!");
    }

}
