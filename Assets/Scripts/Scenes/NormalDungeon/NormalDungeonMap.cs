using UnityEngine;

public class NormalDungeonMap : MonoBehaviour
{
    [SerializeField] private Transform[] _endingPoints;
    [SerializeField] private Transform _cameraPoint;
    

    public Transform[] GetTransforms() { return _endingPoints; }
    public Transform GetCameraPoint() { return _cameraPoint; }

}
