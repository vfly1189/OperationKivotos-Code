using UnityEngine;

public class BossDungeonMap : MonoBehaviour
{
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _cameraPoint;
    [SerializeField] private Transform _bossSpawnPoint;

    [SerializeField] private Transform[] _endingPoints;
    [SerializeField] private Transform _endingCameraPoint;

    [SerializeField] private Transform[] _monsterSpawnPoints;

    [SerializeField] private Transform[] _lightningPoints;

    public Transform GetCharacterSpawnPoint() { return _spawnPoint; }
    public Transform GetCameraPoint() { return _cameraPoint; }
    public Transform GetBossSpawnPoint() { return _bossSpawnPoint; }

    public Transform[] GetEndingPoints() {  return _endingPoints; }
    public Transform GetEndingCameraPoint() { return _endingCameraPoint; }

    public Transform[] GetMonsterSpawnPoints() { return _monsterSpawnPoints; }

    public Transform[] GetLightningPoints() { return _lightningPoints; }
}
