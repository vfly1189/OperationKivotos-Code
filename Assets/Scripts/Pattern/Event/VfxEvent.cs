using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 점마다 원샷 VFX(총구 화염 · 차지 등) — 피해 없음, 보이기만.
// 위치 · 횟수 · 간격은 PointEmitter. _attachTo를 적으면 그 소켓에 붙여 따라다니게 한다(연사 중 총구 · 조준 중 차지).
// 반환은 프리팹의 AutoReturnToPool이 한다 — 기다리지 않는다.
public class VfxEvent : PatternEvent
{
    [SerializeField] private PointEmitter _emit;
    [SerializeField] private GameObject _vfx;

    [Tooltip("비우면 그 자리에 둔다. 소켓 키를 적으면 그 Transform에 붙인다 (예: Muzzle)")]
    [SerializeField] private string _attachTo;

    protected override UniTask FireAsync(PatternContext ctx, CancellationToken token)
    {
        Transform parent = null;
        if (!string.IsNullOrEmpty(_attachTo) && ctx.sockets != null) ctx.sockets.TryGet(_attachTo, out parent);

        return _emit.Run(ctx, p => Play(p, parent), token);
    }

    private UniTask Play(Pose p, Transform parent)
    {
        GameObject go = SpawnAt(_vfx, p.position, p.rotation);
        if (go != null && parent != null) go.transform.SetParent(parent, worldPositionStays: true);
        return UniTask.CompletedTask;
    }
}
