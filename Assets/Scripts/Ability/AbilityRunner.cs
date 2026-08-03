using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class AbilityRunner
{
    // 쿨다운은 이 Runner(=캐스터)에서만 소유하는 상태. 어빌리티별 종료 시각을 기록한다.
    private readonly Dictionary<AbilityData, float> _cooldownEnd = new();

    // 현재 시각 소스. 기본은 Time.time(UnityClock)이라 동작은 이전과 동일하다.
    // 테스트/선택적 시간 제어가 필요하면 생성자로 다른 IClock을 주입한다.
    private readonly IClock _clock;

    public AbilityRunner(IClock clock = null) => _clock = clock ?? UnityClock.Instance;

    public bool IsOnCooldown(AbilityData a)
        => _cooldownEnd.TryGetValue(a, out float end) && _clock.Now < end;

    private void StartCooldown(AbilityData a)
        => _cooldownEnd[a] = _clock.Now + a.Cooldown;

    // token = 캐스터의 액션 토큰(몹 _monsterCts / 캐릭터 _actionCts). 캐스터가 죽거나 상태가 바뀌면 캐스트 중단.
    public async UniTask TryCast(AbilityData ability, AbilityContext ctx, CancellationToken token)
    {
        if (ability == null || IsOnCooldown(ability)) return;   // 쿨다운 검사

        // 태그 게이트: 요구 태그를 전부 갖고, 차단 태그를 하나도 갖지 않아야 발동.
        // ctx.Tags == null(태그 미도입 캐스터)이거나 목록이 비면 제약 없음 → 통과.
        GameplayTagContainer owned = ctx.Tags;
        if (owned != null &&
            (!owned.HasAll(ability.RequiredTags) || !owned.HasNone(ability.BlockedTags)))
            return;

        StartCooldown(ability);

        // 발동(캐스트) 동안 부여할 태그 — 채널링 중 다른 발동을 막는 State.Casting 등.
        // 이펙트가 도중에 취소돼도 반드시 해제되도록 try/finally로 감싼다.
        List<GameplayTagSO> granted = ability.GrantsTags;
        bool hasGrants = owned != null && granted != null && granted.Count > 0;
        if (hasGrants)
            for (int i = 0; i < granted.Count; i++) owned.Add(granted[i]);

        try
        {
            if (ability.CastTime > 0f)                          // 캐스트타임 대기
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
        finally
        {
            if (hasGrants)
                for (int i = 0; i < granted.Count; i++) owned.Remove(granted[i]);
        }
    }
}
