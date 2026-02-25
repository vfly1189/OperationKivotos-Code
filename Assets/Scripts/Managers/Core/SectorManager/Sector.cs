using System.Collections.Generic;
using UnityEngine;

public class Sector : MonoBehaviour
{
    [field: SerializeField] public int SectorId { get; private set; }

    // 자기 구역 내의 스포너들을 리스트로 관리
    [SerializeField] private List<MonsterSpawner> _spawners = new List<MonsterSpawner>();

    private void Start()
    {
        // 하위 오브젝트에 있는 스포너들을 자동 등록
        //_spawners.AddRange(GetComponentsInChildren<MonsterSpawner>());

        // 매니저에 자신을 등록
        Managers.Sector.RegisterSector(this);
    }

    public void ActivateSector()
    {
        //foreach (var spawner in _spawners)
        //{
        //    spawner.SpawnMonsters();
        //}
    }

    public void DeactivateSector()
    {
        //foreach (var spawner in _spawners)
        //{
        //    spawner.DespawnMonsters(); // 혹은 제자리 복귀 명령
        //}
    }

    // 플레이어 진입 감지 (구역 진입 트리거)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Managers.Sector.OnPlayerEnterSector(SectorId);
        }
    }
}
