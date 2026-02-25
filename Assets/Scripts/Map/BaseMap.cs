using UnityEngine;

public class BaseMap : MonoBehaviour
{
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private Transform _shopMasterTr;


    public Transform GetPlayerSpawnPoint() {  return _playerSpawnPoint; }
    public Transform GetShopMasterTr() { return _shopMasterTr; }
}
