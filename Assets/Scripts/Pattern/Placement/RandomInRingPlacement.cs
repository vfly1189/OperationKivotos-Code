using System.Collections.Generic;
using UnityEngine;

// 기준점 둘레 고리(minR ~ maxR) 안 무작위 점. minR = 0이면 원 안 전체, minR > 0이면 그 바깥만.
public class RandomInRingPlacement : Placement
{
    [SerializeField] private float _minRadius = 0f;
    [SerializeField] private float _maxRadius = 5f;
    [SerializeField] private int _count = 6;
    [SerializeField] private PointFacing _pointFacing = PointFacing.Inherit;

    protected override void Generate(Pose origin, List<Pose> results)
    {
        float min2 = _minRadius * _minRadius;
        float max2 = _maxRadius * _maxRadius;

        for (int i = 0; i < _count; i++)
        {
            // 넓이 기준 균등 — 반지름을 그냥 고르면 중심에 몰린다
            float r = Mathf.Sqrt(Random.Range(min2, max2));
            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            results.Add(new Pose(origin.position + dir * r, Face(origin.rotation, dir, _pointFacing)));
        }
    }
}
