using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

// 점마다 내려치기 — VFX + 판정(HitShape). 한 번이든 동심원 48번이든 PointEmitter 설정 차이일 뿐이다.
// _delay > 0 이면 경고(_warning)를 먼저 깔고 그 시간 뒤 같은 자리를 친다(Tank 포격 · 경고 → 타격).
//  지연 타격은 점마다 따로 기다려 다음 점을 막지 않는다. 중복 제거도 점마다 — Fire 단위 집합은 Run이 끝나면 반납되므로.
public class StrikeEvent : PatternEvent
{
    [SerializeField] private PointEmitter _emit;
    [SerializeField] private GameObject _vfx;                // 원샷 VFX

    [Tooltip("프리팹 자체 자세 보정 — 점의 방향(Placement)과 별개")]
    [SerializeField] private Vector3 _vfxEuler;

    [Tooltip("초. 0 = 프리팹이 스스로 회수(AutoReturnToPool). 회수 장치가 없는 VFX면 적는다")]
    [SerializeField] private float _vfxLifetime = 0f;

    [Header("경고 (선택)")]
    [Tooltip("초. 0 = 경고 없이 바로 친다")]
    [SerializeField] private float _delay = 0f;
    [SerializeField] private GameObject _warning;            // 바닥 데칼 — 지연이 끝나면 이 Event가 회수
    [SerializeField] private Vector3 _warningEuler;

    [SerializeField] private DamageSpec _damage;
    [SerializeField] private HitShape _hit;

    protected override async UniTask FireAsync(PatternContext ctx, CancellationToken token)
    {
        DamageInfo packet = _damage.Pack(ctx);

        if (_delay > 0f)
        {
            await _emit.Run(ctx, p => { DelayedStrike(p, packet, token).Forget(); return UniTask.CompletedTask; }, token);
            return;
        }

        var hitSet = HashSetPool<IDamageable>.Get();
        try { await _emit.Run(ctx, p => Strike(p, packet, hitSet), token); }
        finally { HashSetPool<IDamageable>.Release(hitSet); }
    }

    private UniTask Strike(Pose p, DamageInfo packet, HashSet<IDamageable> hitSet)
    {
        GameObject fx = SpawnAt(_vfx, p.position, p.rotation * Quaternion.Euler(_vfxEuler));
        if (fx != null && _vfxLifetime > 0f) Expire(fx, _vfxLifetime).Forget();

        _hit.Apply(packet, p.position, hitSet);
        return UniTask.CompletedTask;
    }

    // 경고 → 기다림 → 같은 자리 타격. 취소(사망 · 회수)면 경고만 치우고 치지 않는다.
    private async UniTaskVoid DelayedStrike(Pose p, DamageInfo packet, CancellationToken token)
    {
        GameObject warn = SpawnAt(_warning, p.position, p.rotation * Quaternion.Euler(_warningEuler));

        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(_delay), cancellationToken: token).SuppressCancellationThrow();
        if (warn != null && warn.activeSelf) Managers.Resource.Destroy(warn);
        if (canceled) return;

        Strike(p, packet, null);
    }

    // 회수 장치 없는 VFX — 시전자가 죽어도 연출은 끝까지 (토큰 대신 VFX 자신의 파괴 토큰)
    private static async UniTaskVoid Expire(GameObject go, float seconds)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: go.GetCancellationTokenOnDestroy())
                     .SuppressCancellationThrow();
        if (go != null && go.activeSelf) Managers.Resource.Destroy(go);
    }
}
