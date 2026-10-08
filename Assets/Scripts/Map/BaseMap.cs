using UnityEngine;

public class BaseMap : MonoBehaviour
{
    [SerializeField] private int _mapID;
    
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private Transform _shopMasterTr;

    public int MapID => _mapID;

    public MonsterSpawner[] GetSpawners() => GetComponentsInChildren<MonsterSpawner>(true);
    public Sector[] GetSectors() => GetComponentsInChildren<Sector>(true);
    public Transform GetPlayerSpawnPoint() {  return _playerSpawnPoint; }
    public Transform GetShopMasterTr() { return _shopMasterTr; }
}
