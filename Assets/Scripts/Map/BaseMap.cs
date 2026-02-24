using UnityEngine;

public class BaseMap : MonoBehaviour
{
    [SerializeField] private Transform _playerSpawnPoint;


    public Transform GetPlayerSpawnPoint() {  return _playerSpawnPoint; }
}
