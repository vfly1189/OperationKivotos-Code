using System.Collections.Generic;
using UnityEngine;

public class Sector : MonoBehaviour
{
    [SerializeField] public int _sectorID;

    // 자기 구역 내의 스포너들을 리스트로 관리
    private List<MonsterSpawner> _spawners = new List<MonsterSpawner>();
    private bool _isInitialized = false;

    private void Start()
    {
        // TODO : 하위 오브젝트에 있는 스포너들을 자동 등록
        InitSpawners();
        Managers.Sector.RegisterSector(this);
    }

    // [방어 코드 1] 외부에서 호출될 수도 있으므로 초기화 여부 체크
    private void InitSpawners()
    {
        if (_isInitialized) return;

        _spawners.Clear();
        _spawners.AddRange(GetComponentsInChildren<MonsterSpawner>(true));
        _isInitialized = true;
    }

    public void ActivateSector()
    {
        Debug.Log($"{_sectorID} ActiveSector 실행");
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
            Managers.Sector.OnPlayerEnterSector(_sectorID);
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
