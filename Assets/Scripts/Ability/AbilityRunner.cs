using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class AbilityRunner
{
    private readonly Dictionary<AbilityData, (float end, float dur)> _cd = new();
    // 쿨다운은 이 Runner(=캐스터)에서만 소유하는 상태. 어빌리티별 종료 시각을 기록한다.
    //private readonly Dictionary<AbilityData, float> _cooldownEnd = new();

    // 현재 시각 소스. 기본은 Time.time(UnityClock)이라 동작은 이전과 동일하다.
    // 테스트/선택적 시간 제어가 필요하면 생성자로 다른 IClock을 주입한다.
    private readonly IClock _clock;

    public AbilityRunner(IClock clock = null) => _clock = clock ?? UnityClock.Instance;

    public bool IsOnCooldown(AbilityData a) => _cd.TryGetValue(a, out var value) && _clock.Now < value.end;

    private void StartCooldown(AbilityData a, AbilityContext ctx)
    {
        float cooldown = ctx.CoolDown >= 0f ? ctx.CoolDown : a.Cooldown;
        _cd[a] = (_clock.Now + cooldown, cooldown);
    }

    public float CooldownRemaining(AbilityData a)
    {
        _cd.TryGetValue(a, out var value);

        return Mathf.Max(0f, value.end - _clock.Now);
    }
    public float CooldownDuration(AbilityData a) => _cd.TryGetValue(a, out var c) ? c.dur : 0f;


    // ── admission(게이트+커밋) — 통과 시 쿨을 시작한다. 이펙트는 실행하지 않는다.
    // 입력 시점에 상태 전이를 결정할 때 쓴다(예: Q는 컷신 진입 전에 여기서 쿨/궁게이지 확정).
    // 쿨다운 → 태그(Required 전부·Blocked 없음) 순서. 하나라도 실패하면 부작용 0으로 false.
    public bool Commit(AbilityData ability, AbilityContext ctx)
    {
        if (ability == null || IsOnCooldown(ability)) return false;   // 쿨다운 검사

        // 태그 게이트: 요구 태그를 전부 갖고, 차단 태그를 하나도 갖지 않아야 발동.
        // ctx.Tags == null(태그 미도입 캐스터)이거나 목록이 비면 제약 없음 → 통과.
        GameplayTagContainer owned = ctx.Tags;
        if (owned != null &&
            (!owned.HasAll(ability.RequiredTags) || !owned.HasNone(ability.BlockedTags)))
            return false;

        StartCooldown(ability, ctx);
        return true;
    }

    // ── fire(비게이트) — 캐스트타임 대기 → 이펙트 순차 실행 → GrantsTags try/finally 해제.
    // 게이트/쿨 재검사 없음(이미 Commit됨). 평타·Q는 애니 비트가, E·몹은 즉시 이걸 부른다.
    // token = 캐스터의 액션 토큰(몹 _monsterCts / 캐릭터 _actionCts). 캐스터가 죽거나 상태가 바뀌면 캐스트 중단.
    public async UniTask Fire(AbilityData ability, AbilityContext ctx, CancellationToken token)
    {
        if (ability == null) return;

        // 발동(캐스트) 동안 부여할 태그 — 채널링 중 다른 발동을 막는 State.Casting 등.
        // 이펙트가 도중에 취소돼도 반드시 해제되도록 try/finally로 감싼다.
        GameplayTagContainer owned = ctx.Tags;
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

    // ── 편의: Commit+Fire를 한 순간에. 몹/보스처럼 게이트와 발사가 같은 순간인 호출부용(호출부 무변경).
    public async UniTask TryCast(AbilityData ability, AbilityContext ctx, CancellationToken token)
    {
        if (!Commit(ability, ctx)) return;
        await Fire(ability, ctx, token);
    }
}
