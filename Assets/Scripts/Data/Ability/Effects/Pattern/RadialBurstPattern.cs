using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 방사형(여러 방향)으로 지점을 만들되 웨이브마다 반경을 보간(밖→안 수렴 등).
// '어디에(중심)'는 공용 Anchor, '무엇을'은 자식(_children)이 담당.
[CreateAssetMenu(menuName = "Kivotos/Effect/RadialBurstPattern")]
public class RadialBurstPattern : EffectData, IEffect
{
    [SerializeField] private Anchor _center = new();   // 방사 중심
    [SerializeField] private int _directions = 4;      // 한 웨이브의 방향 수
    [SerializeField] private int _waves = 5;           // 수렴 단계
    [SerializeField] private float _startRadius = 8f;  // 바깥에서 시작
    [SerializeField] private float _endRadius = 0f;    // 중심으로 수렴
    [SerializeField] private float _angleStep = 0f;    // 웨이브마다 방향 회전
    [SerializeField] private float _interval = 0.25f;  // 웨이브 간격
    [SerializeField] private List<EffectData> _children = new(); // 각 지점 payload

    public override IEffect CreateRuntime() => this;

    public async UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        var payload = new List<IEffect>(_children.Count);
        foreach (var c in _children) if (c) payload.Add(c.CreateRuntime());

        _center.Resolve(ctx, out Vector3 center, out _);

        for (int w = 0; w < _waves; w++)
        {
            float t = _waves <= 1 ? 0f : (float)w / (_waves - 1);
            float radius = Mathf.Lerp(_startRadius, _endRadius, t);

            for (int d = 0; d < _directions; d++)
            {
                if (token.IsCancellationRequested) return;
                float ang = _angleStep * w + 360f / _directions * d;
                Vector3 point = center + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * radius;

                var sub = ctx.CloneAt(point);
                foreach (var e in payload) await e.ExecuteAsync(sub, token);
            }

            if (_interval > 0f && w < _waves - 1)
            {
                bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(_interval),
                    cancellationToken: token).SuppressCancellationThrow();
                if (canceled) return;
            }
        }
    }
}
