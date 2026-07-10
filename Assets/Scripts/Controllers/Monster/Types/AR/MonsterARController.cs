using Cysharp.Threading.Tasks;
using System.Collections;
using System.Threading;
using UnityEngine;

public class MonsterARController : RangedMonsterController
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject _bulletPrefab; // 풀링용 프리팹 (Poolable 필수)
    [SerializeField] private GameObject _bulletFire;   // 총구화염
    [SerializeField] private Transform _firePoint;     // 총구 위치

    [Header("Rapid Fire Settings")]
    [SerializeField] private int _shotCount = 5;       // 한 번 공격에 나가는 총알 수
    [SerializeField] private float _fireDelay = 0.05f;  // 총알 사이 간격 (초)

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
