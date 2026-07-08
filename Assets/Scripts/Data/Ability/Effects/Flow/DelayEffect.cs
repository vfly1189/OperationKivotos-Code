using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

// 지정 시간만큼 대기하는 프리미티브. Effect 체인 '중간'에서 순서를 제어한다.
// (CastTime은 모든 Effect '앞'에서만 대기 → "먼저 뭔가 하고, 기다렸다가, 발사"는 표현 못 함)
//   예: 차징샷 = [ SpawnVFX(charge), DelayEffect(1), SpawnProjectiles ]  ← charge 후 1초 뒤 발사
// 상태 없음 → CreateRuntime() => this (공유 안전).
[CreateAssetMenu(menuName = "Kivotos/Effect/Delay")]
public class DelayEffect : EffectData, IEffect
{
    [SerializeField] private float _seconds = 1f;

    public override IEffect CreateRuntime() => this;

    public async UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        if (_seconds <= 0f) return;
        await UniTask.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken: token)
            .SuppressCancellationThrow();
    }
}
