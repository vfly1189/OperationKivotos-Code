using System.Collections.Generic;
using UnityEngine;

// 기준점 그 자리 하나
public class PointPlacement : Placement
{
    protected override void Generate(Pose origin, List<Pose> results) => results.Add(origin);
}
