using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;

public class MonsterRLController : RangedMonsterController
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject _bulletPrefab; // 풀링용 프리팹 (Poolable 필수)
    [SerializeField] private Transform _firePoint;     // 총구 위치
    [SerializeField] private ParticleSystem _rocketFireEffect;

    protected override void PerformAttackAction()
    {
        var ctx = new AbilityContext
        {
            Caster = this,
            CasterGO = gameObject,
            CasterStat = Stat,
            Object = _firePoint,   // ★ 총구 — 총알 스폰 위치
            Target = null,      // 조준/방향용
        };
        _abilityRunner.TryCast(_abilities[0], ctx, _monsterCts.Token).Forget();
    }
}
