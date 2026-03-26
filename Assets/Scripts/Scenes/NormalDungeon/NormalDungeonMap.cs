using UnityEngine;

public class NormalDungeonMap : BaseMap, IDungeonMap
{
    [SerializeField] private Transform[] _endingPoints;
    [SerializeField] private Transform _endingCameraPoint;
    

    public Transform[] GetEndingTransforms() { return _endingPoints; }
    public Transform GetEndingCameraPoint() { return _endingCameraPoint; }

}
