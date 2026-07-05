using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 지연형 범위 폭격: 착탄점(ctx.TargetPoint)에 경고 표시 → 딜레이 → 폭발 → 범위 데미지.
// 인스턴스 필드 없음(전부 로컬) → CreateRuntime() => this 로 공유 안전.
// 경고/폭발은 '풀링 미적용' — 그냥 Instantiate / Destroy.
// (Instantiate·Destroy는 ScriptableObject가 상속한 UnityEngine.Object의 정적 멤버라 그대로 호출)
[CreateAssetMenu(menuName = "Kivotos/Effect/AreaStrike")]
public class AreaStrike : EffectData, IEffect
{
    [Header("Prefabs")]
    [SerializeField] private GameObject _warningPrefab;    // 경고 데칼
    [SerializeField] private GameObject _explosionPrefab;  // 폭발 VFX
    [SerializeField] private float _explosionLifetime = 2f; // 폭발 이펙트 자동 삭제까지(초)

    [Header("Decal 회전 (바닥에 눕히기)")]
    [Tooltip("데칼이 길쭉하게 서면 X를 90/-90으로 조정")]
    [SerializeField] private Vector3 _warningEuler = new Vector3(90f, 0f, 0f);

    [Header("Params")]
    [SerializeField] private float _delay = 1f;      // 경고 → 폭발
    [SerializeField] private float _radius = 1f;     // 폭발 반경
    [SerializeField] private float _damageMul = 1f;
    [SerializeField] private LayerMask _hitMask;     // Player
    [SerializeField] private LayerMask _groundMask;  // MapGround

    public override IEffect CreateRuntime() => this;

    public async UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        Vector3 pos = SnapToGround(ctx.TargetPoint);   // 캐스터(탱크)가 준 착탄점을 지면에 스냅

        // 1) 경고 데칼 (바닥에 눕히는 회전)
        GameObject warn = _warningPrefab != null
            ? Instantiate(_warningPrefab, pos, Quaternion.Euler(_warningEuler))
            : null;

        // 2) 경고 시간 대기 (취소 시 경고 제거 후 종료)
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(_delay),
            cancellationToken: token).SuppressCancellationThrow();
        if (warn != null) Destroy(warn);
        if (canceled) return;

        // 3) 폭발 VFX (수명 뒤 자동 삭제)
        if (_explosionPrefab != null)
        {
            GameObject fx = Instantiate(_explosionPrefab, pos, Quaternion.identity);
            Destroy(fx, _explosionLifetime);
        }

        // 4) 범위 데미지
        foreach (var col in Physics.OverlapSphere(pos, _radius, _hitMask))
        {
            if (col.TryGetComponent<IDamageable>(out var target))
            {
                var dmg = new DamageInfo(ctx.CasterStat.Attack.Value * _damageMul, ctx.CasterGO, false);
                dmg.HitPoint = col.ClosestPoint(pos);
                target.TakeDamage(dmg);
            }
        }
    }

    private Vector3 SnapToGround(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, _groundMask))
            return hit.point + Vector3.up * 0.05f;   // Z-Fighting 방지
        return p;
    }
}
