using UnityEngine;

public class BossDungeonMap : MonoBehaviour
{
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _cameraPoint;
    [SerializeField] private Transform _bossSpawnPoint;

    public Transform GetCharacterSpawnPoint() { return _spawnPoint; }
    public Transform GetCameraPoint() { return _cameraPoint; }
    public Transform GetBossSpawnPoint() { return _bossSpawnPoint; }

}
