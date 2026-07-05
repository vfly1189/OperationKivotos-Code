using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 원/도넛 영역에 N개 지점을 뿌리고, 각 지점에서 자식 payload 를 실행한다.
// '어디에(중심)'는 공용 Anchor 가, '무엇을'은 자식(_children)이 담당.
[CreateAssetMenu(menuName = "Kivotos/Effect/ScatterPattern")]
public class ScatterPattern : EffectData, IEffect
{
    [SerializeField] private Anchor _center = new();    // 산포 중심
    [SerializeField] private float _innerRadius = 0f;   // 안쪽(0이면 꽉 찬 원)
    [SerializeField] private float _outerRadius = 2f;   // 바깥
    [SerializeField] private int _count = 8;
    [SerializeField] private float _interval = 0.05f;
    [SerializeField] private List<EffectData> _children = new(); // 각 지점 payload

    public override IEffect CreateRuntime() => this;

    public async UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        var payload = new List<IEffect>(_children.Count);
        foreach (var c in _children) if (c) payload.Add(c.CreateRuntime());

        _center.Resolve(ctx, out Vector3 center, out _);

        for (int i = 0; i < _count; i++)
        {
            if (token.IsCancellationRequested) return;

            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            // 면적 균일 분포: r = sqrt(random(inner^2, outer^2))
            float r = Mathf.Sqrt(UnityEngine.Random.Range(_innerRadius * _innerRadius,
                                                          _outerRadius * _outerRadius));
            Vector3 point = center + new Vector3(dir.x, 0f, dir.y) * r;

            var sub = ctx.CloneAt(point);
            foreach (var e in payload) await e.ExecuteAsync(sub, token);

            if (_interval > 0f && i < _count - 1)
                if (await UniTask.Delay(TimeSpan.FromSeconds(_interval),
                    cancellationToken: token).SuppressCancellationThrow()) return;
        }
    }
}
