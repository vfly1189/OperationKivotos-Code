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

    protected override void Generate(Pose origin, List<Pose> results)
    {
        if (_countPerRing <= 0) return;

        float step = 360f / _countPerRing * (_clockwise ? 1f : -1f);
        foreach (float r in _radii)
        {
            for (int i = 0; i < _countPerRing; i++)
            {
                Vector3 dir = origin.rotation * (Quaternion.Euler(0f, _startAngle + step * i, 0f) * Vector3.forward);
                results.Add(new Pose(origin.position + dir * r, Face(origin.rotation, dir, _pointFacing)));
            }
        }
    }
}
