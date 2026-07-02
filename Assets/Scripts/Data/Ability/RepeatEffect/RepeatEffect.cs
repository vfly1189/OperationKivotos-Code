using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// '반복'이라는 로직을 독립 부품으로 뺀 컨테이너 Effect.
// 자식 Effect들을 _count 번, _interval 간격으로 실행한다.
// 무엇을 반복할지는 조합(_children)으로 정한다.
//   예: 연발 = RepeatEffect{ count5, interval0.1, children:[ SpawnProjectiles(count1), SpawnVFX(muzzle) ] }
// → 매 회차마다 '총알 1발 + 총구 화염 1번'이 같이 나가고, 연발 타이밍은 여기 한 곳에만 존재한다.
//
// 상태 없음(루프 변수·런타임 리스트 전부 로컬) → CreateRuntime() => this (공유 안전).
[CreateAssetMenu(menuName = "Kivotos/Effect/Repeat")]
public class RepeatEffect : EffectData, IEffect
{
    [SerializeField] private int _count = 1;
    [SerializeField] private float _interval = 0.1f;

    [Header("매 회차 실행할 자식 Effect들 (순서대로)")]
    [SerializeField] private List<EffectData> _children = new();

    public override IEffect CreateRuntime() => this;

    public async UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        // 자식들을 런타임 IEffect로 1회 변환해 반복 동안 재사용
        var effects = new List<IEffect>(_children.Count);
        foreach (var child in _children)
            if (child != null) effects.Add(child.CreateRuntime());

        for (int i = 0; i < _count; i++)
        {
            for (int e = 0; e < effects.Count; e++)
            {
                if (token.IsCancellationRequested) return;
                await effects[e].ExecuteAsync(ctx, token);
            }

            if (_interval > 0f && i < _count - 1) // 마지막 회차 뒤엔 안 기다림
            {
                bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(_interval),
                    cancellationToken: token).SuppressCancellationThrow();
                if (canceled) return;
            }
        }
    }
}
