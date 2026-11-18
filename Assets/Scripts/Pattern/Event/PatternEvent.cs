using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// "무엇을" — 구체 Event는 이걸 한 번만 상속한다. "어디에 · 간격"은 상속이 아니라 PointEmitter 필드로 조합.
public abstract class PatternEvent : MonoBehaviour
{
    // 부르는 쪽(시그널, EventAction)은 이것만 앎 — 쏘고 잊기
    // 기다릴 게 없는 Event는 이 호출 안에서 바로 끝난다 → Task 안 순서가 그대로 지켜진다
    [Tooltip("신호를 받고 몇 초 뒤에 할까. 0 = 바로 (같은 Task 안 순서 그대로)")]
    [SerializeField, Min(0f)] protected float _startDelay = 0f;

    public void Fire(PatternContext ctx)
    {
        if (_startDelay <= 0f) FireAsync(ctx, ctx.cToken).Forget();
        else FireLater(ctx).Forget();
    }

    private async UniTaskVoid FireLater(PatternContext ctx)
    {
        if (await UniTask.Delay(System.TimeSpan.FromSeconds(_startDelay), cancellationToken: ctx.cToken)
                         .SuppressCancellationThrow()) return;   // 기다리는 동안 패턴이 끊김
        await FireAsync(ctx, ctx.cToken);
    }

    // 구현은 여기 — 기다릴 게 없는 이벤트는 UniTask.CompletedTask 반환
    protected abstract UniTask FireAsync(PatternContext ctx, CancellationToken token);

    // 풀에서 꺼내 그 자리에 놓고 재생. 풀 재사용 대비 Clear 후 Play (SpawnVFX와 같은 이유)
    protected static GameObject SpawnAt(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;

        var go = Managers.Resource.Instantiate(prefab, pos, rot);
        if (go != null && go.GetComponentInChildren<ParticleSystem>(true) is { } ps)
        {
            ps.Clear(true);
            ps.Play(true);
        }
        return go;
    }
}
