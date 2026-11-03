using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 점마다 투사체 발사 — 움직이는 물체라 판정은 투사체 자신(콜라이더)이 한다. HitShape 없음.
// 패킷은 Fire 때 한 번(DamageSpec) → 투사체에 넘긴다. 연사는 PointEmitter의 _repeat · _interval.
public class ProjectileEvent : PatternEvent
{
    [SerializeField] private PointEmitter _emit;
    [SerializeField] private GameObject _projectile;      // IProjectile
    [SerializeField] private GameObject _muzzleVfx;       // 선택 — 점마다 원샷 VFX (총구 화염)

    [SerializeField] private DamageSpec _damage;

    protected override UniTask FireAsync(PatternContext ctx, CancellationToken token)
    {
        DamageInfo packet = _damage.Pack(ctx);
        GameObject shooter = ctx.owner.gameObject;
        return _emit.Run(ctx, p => Shoot(p, packet, shooter), token);
    }

    private UniTask Shoot(Pose p, DamageInfo packet, GameObject shooter)
    {
        GameObject go = Managers.Resource.Instantiate(_projectile, p.position, p.rotation);
        if (go != null && go.TryGetComponent(out IProjectile projectile))
            projectile.Init(packet, shooter);

        SpawnAt(_muzzleVfx, p.position, p.rotation);
        return UniTask.CompletedTask;
    }
}
