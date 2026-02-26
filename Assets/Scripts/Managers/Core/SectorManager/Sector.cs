using System.Collections.Generic;
using UnityEngine;

public class Sector : MonoBehaviour
{
    [field: SerializeField] public int SectorId { get; private set; }

    // 자기 구역 내의 스포너들을 리스트로 관리
    private List<MonsterSpawner> _spawners = new List<MonsterSpawner>();

    private void Start()
    {
        // TODO : 하위 오브젝트에 있는 스포너들을 자동 등록
        _spawners.AddRange(GetComponentsInChildren<MonsterSpawner>(true));

        // 매니저에 자신을 등록
        Managers.Sector.RegisterSector(this);
    }

    public void ActivateSector()
    {
        Debug.Log($"{SectorId} ActiveSector 실행");
        foreach (var spawner in _spawners)
        {
            spawner.SpawnMonsters();
        }
    }

    public void DeactivateSector()
    {
        foreach (var spawner in _spawners)
        {
            spawner.DespawnMonsters(); // 혹은 제자리 복귀 명령
        }
    }

    // 플레이어 진입 감지 (구역 활성화)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Managers.Sector.OnPlayerEnterSector(SectorId);
            //ActivateSector();
        }
    }

    // 플레이어 이탈 감지 (구역 비활성화)
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Managers.Sector.OnPlayerExitSector(SectorId);
            //DeactivateSector();
        }
    }

#if UNITY_EDITOR
    // 씬 뷰에서 Sector의 영역을 시각적으로 확인하기 위한 기즈모
    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f); // 반투명 초록색
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(col.center, col.size);
        }
    }
#endif
}
