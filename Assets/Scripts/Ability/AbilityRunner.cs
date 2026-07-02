using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class AbilityRunner
{
    // 쿨다운은 이 Runner(=캐스터)마다 개별로 관리. 종료 시각을 기록.
    private readonly Dictionary<AbilityData, float> _cooldownEnd = new();

    public bool IsOnCooldown(AbilityData a)
        => _cooldownEnd.TryGetValue(a, out float end) && Time.time < end;

    private void StartCooldown(AbilityData a)
        => _cooldownEnd[a] = Time.time + a.Cooldown;

    // token = 캐스터의 수명 토큰(몬스터 _monsterCts.Token / 캐릭터 토큰)  죽으면 캐스트 중단
    public async UniTask TryCast(AbilityData ability, AbilityContext ctx, CancellationToken token)
    {
        if (ability == null || IsOnCooldown(ability)) return;   // 조건 검사
        StartCooldown(ability);

        if (ability.CastTime > 0f)                              // 캐스트타임 대기
        {
            bool canceled = await UniTask.Delay(
                TimeSpan.FromSeconds(ability.CastTime), cancellationToken: token)
                .SuppressCancellationThrow();
            if (canceled) return;
        }

        foreach (var effect in ability.BuildRuntimeEffects())  // Effect 순서대로 실행
        {
            if (token.IsCancellationRequested) return;
            await effect.ExecuteAsync(ctx, token);
        }
    }
}
