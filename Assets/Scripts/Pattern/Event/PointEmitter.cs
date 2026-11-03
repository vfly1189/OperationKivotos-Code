using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

// "어디에 · 어떤 간격으로" — 점을 쓰는 Event들이 필드로 들고 쓰는 공용 부품(상속 대신 조합).
// Collect는 회차마다 한 번 → 위치는 그때 고정된다.
// 간격 대기 · 대기 뒤 토큰 확인 · 점 목록 반납은 전부 여기서 — 각 Event는 "점 하나에 무엇을"만 쓴다.
[Serializable]
public class PointEmitter
{
    [SerializeField] private Placement _where;

    [Tooltip("묶음 사이 간격(초). 0 = 전부 동시")]
    [SerializeField] private float _interval = 0f;

    [Tooltip("한 번에 몇 개씩. 0 = 전부. 예: 원 12개 × 4바퀴를 바퀴 단위로 → 12")]
    [SerializeField] private int _batch = 1;

    [Tooltip("같은 Placement를 몇 번 돌까. 연사 = 총구 점 하나를 N번. 회차마다 점을 다시 구한다(움직이는 소켓을 따라감)")]
    [SerializeField, Min(1)] private int _repeat = 1;

    [Tooltip("비우면 저장 안 함. 적으면 memo에 점을 저장 → 다른 Event가 Anchor = Saved로 기준점으로 쓴다")]
    [SerializeField] private string _saveAs;

    public async UniTask Run(PatternContext ctx, Func<Pose, UniTask> at, CancellationToken token)
    {
        if (_where == null)
        {
            Debug.LogWarning("[PointEmitter] Placement가 비어 있음");
            return;
        }

        // 간격 대기 너머까지 쓰므로 필드 재사용 금지(같은 Event가 겹쳐 쏘이면 덮어씀) — 풀에서 빌리고 반납
        List<Pose> points = ListPool<Pose>.Get();
        try
        {
            int repeat = Mathf.Max(1, _repeat);
            for (int r = 0; r < repeat; r++)
            {
                points.Clear();
                _where.Collect(ctx, points);
                if (points.Count == 0) return;

                if (r == 0 && !string.IsNullOrEmpty(_saveAs) && ctx.memo != null)
                    ctx.memo.SavePoints(_saveAs, points);

                int step = (_interval <= 0f || _batch <= 0) ? points.Count : _batch;
                for (int i = 0; i < points.Count; i++)
                {
                    if (token.IsCancellationRequested) return;   // 대기 중 사망 · 회수 — 풀 재사용된 시전자를 건드리지 않게

                    await at(points[i]);

                    bool last = r == repeat - 1 && i + 1 == points.Count;
                    bool groupEnd = _interval > 0f && !last && ((i + 1) % step == 0 || i + 1 == points.Count);
                    if (groupEnd &&
                        await UniTask.Delay(TimeSpan.FromSeconds(_interval), cancellationToken: token)
                                     .SuppressCancellationThrow()) return;
                }
            }
        }
        finally
        {
            ListPool<Pose>.Release(points);
        }
    }
}
