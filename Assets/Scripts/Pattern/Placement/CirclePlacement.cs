using System.Collections.Generic;
using UnityEngine;

// 기준점 둘레 원. 반지름을 여러 개 주면 동심원 — 넣은 순서대로 한 바퀴씩 (예: 8 → 6 → 4 → 2 = 바깥에서 조여 옴)
public class CirclePlacement : Placement
{
    [SerializeField] private float[] _radii = { 5f };
    [SerializeField] private int _countPerRing = 12;
    [Tooltip("첫 점의 각도 — 0 = 기준점 앞")]
    [SerializeField] private float _startAngle = 0f;
    [SerializeField] private bool _clockwise = true;
    [SerializeField] private PointFacing _pointFacing = PointFacing.Outward;

    [Tooltip("펼칠 각도. 360 = 한 바퀴(끝점 안 겹침). 그 밖 = _startAngle부터 양 끝 포함 (정면 180° 부채꼴 = start −90 · arc 180)")]
    [SerializeField, Range(0f, 360f)] private float _arc = 360f;

    [Tooltip("점마다 무작위로 흔들 각도(±도). 0 = 고르게. 회차마다 다시 구하므로 탄막 '흩뿌리기'가 된다")]
    [SerializeField, Min(0f)] private float _angleJitter = 0f;

    protected override void Generate(Pose origin, List<Pose> results)
    {
        if (_countPerRing <= 0) return;

        float step = _arc >= 360f
            ? 360f / _countPerRing
            : (_countPerRing > 1 ? _arc / (_countPerRing - 1) : 0f);
        step *= _clockwise ? 1f : -1f;

        // 흔들림은 플레이 중에만 — 기즈모 미리보기가 떨리지 않게
        float jitter = Application.isPlaying ? _angleJitter : 0f;

        foreach (float r in _radii)
        {
            for (int i = 0; i < _countPerRing; i++)
            {
                float angle = _startAngle + step * i;
                if (jitter > 0f) angle += Random.Range(-jitter, jitter);

                Vector3 dir = origin.rotation * (Quaternion.Euler(0f, angle, 0f) * Vector3.forward);
                results.Add(new Pose(origin.position + dir * r, Face(origin.rotation, dir, _pointFacing)));
            }
        }
    }
}
