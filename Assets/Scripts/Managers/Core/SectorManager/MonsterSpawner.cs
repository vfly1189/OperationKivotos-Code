using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [Header("Spawner Info")]
    [SerializeField] private int _spawnerId;

    // 각 포인트별로 코루틴(리스폰 타이머)을 관리하기 위한 배열
    private Coroutine[] _respawnCoroutines;
    // 각 포인트별로 현재 살아있는 몬스터를 추적하기 위한 배열 (죽었는지 살았는지 확인용)
    private MonsterController[] _spawnedMonsters;

    private List<Transform> _spawnPoints = new List<Transform>();

    // 현재 이 스포너가 관리중인 활성 몬스터 리스트
    private List<MonsterController> _activeMonsters = new List<MonsterController>();

    private void Awake()
    {
        // 리스트 초기화
        _spawnPoints.Clear();

        foreach (Transform child in transform)
        {
            _spawnPoints.Add(child);
        }

        // 스폰 포인트 개수만큼 배열 초기화
        int count = _spawnPoints.Count;
        _respawnCoroutines = new Coroutine[count];
        _spawnedMonsters = new MonsterController[count];
    }

    public void SpawnMonsters()
    {
        SpawnerData data = Managers.Data.GetSpawnerData(_spawnerId);

        if (data == null || data.spawnList == null || data.spawnList.Count == 0)
        {
            Debug.LogWarning($"Spawner [{_spawnerId}]에 등록된 스폰 데이터가 없습니다.");
            return;
        }

        // 각 SpawnInfo(각 포인트의 몬스터 데이터)마다 독립적인 초기 스폰 시작
        foreach (var info in data.spawnList)
        {
            if (info.pointIndex >= _spawnPoints.Count) continue;

            // 이미 해당 포인트에 몬스터가 살아있거나 리스폰 대기 중이면 무시
            if (_spawnedMonsters[info.pointIndex] != null || _respawnCoroutines[info.pointIndex] != null)
            {
                Debug.Log("이거");
                continue;
            }

            // 각 포인트별로 독립적인 스폰/리스폰 사이클 코루틴 시작
            _respawnCoroutines[info.pointIndex] = StartCoroutine(CoSpawnPointRoutine(info));
        }
    }

    public void DespawnMonsters()
    {
        // 모든 코루틴(리스폰 대기 등) 중지
        StopAllCoroutines();

        foreach (var monster in _activeMonsters)
        {
            if (monster != null)
            {
                Managers.Resource.Destroy(monster.gameObject);
            }
        }
        _activeMonsters.Clear();

        // 3. 상태 추적 배열 초기화 (null로 덮어씌우기)
        for (int i = 0; i < _respawnCoroutines.Length; i++)
        {
            _respawnCoroutines[i] = null;
        }

        for (int i = 0; i < _spawnedMonsters.Length; i++)
        {
            _spawnedMonsters[i] = null;
        }
    }

    // 특정 "스폰 포인트 하나"를 전담하는 코루틴
    private IEnumerator CoSpawnPointRoutine(SpawnInfo info)
    {
        int pointIdx = info.pointIndex;
        Transform spawnPoint = _spawnPoints[pointIdx];

        MonsterData monsterData = Managers.Data.GetMonsterDataById(info.monsterId);
        if (monsterData == null) yield break;

        // 설정된 delay만큼 대기 후 소환
        if (info.delay > 0)
            yield return new WaitForSeconds(info.delay);

        // 몬스터 소환
        GameObject monsterObj = Managers.Resource.Instantiate(monsterData.addressableKey, spawnPoint.position, spawnPoint.rotation);

        if (monsterObj != null)
        {
            MonsterController monsterCtrl = monsterObj.GetComponent<MonsterController>();
            MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();

            if (monsterStat != null)
            {
                monsterStat.Init(monsterData);

                monsterStat.OnDead -= () => OnMonsterDead(monsterCtrl, pointIdx, info);
                monsterStat.OnDead += () => OnMonsterDead(monsterCtrl, pointIdx, info);
            }

            if (monsterCtrl != null)
            {
                _spawnedMonsters[pointIdx] = monsterCtrl;
                _activeMonsters.Add(monsterCtrl);
            }
        }

        // 소환 완료했으므로 코루틴 참조는 비워둠
        _respawnCoroutines[pointIdx] = null;
    }

    // 몬스터가 죽었을 때 호출되는 콜백
    private void OnMonsterDead(MonsterController deadMonster, int pointIdx, SpawnInfo info)
    {
        // 리스트에서 제거
        if (_activeMonsters.Contains(deadMonster))
            _activeMonsters.Remove(deadMonster);

        _spawnedMonsters[pointIdx] = null;

        // 이벤트 구독 해제
        MonsterStat stat = deadMonster.GetComponent<MonsterStat>();
        if (stat != null)
        {
            stat.OnDead -= () => OnMonsterDead(deadMonster, pointIdx, info);
        }

        // 죽었으므로 해당 자리에 딜레이(쿨타임) 후 다시 스폰하는 코루틴 재시작
        if (gameObject.activeInHierarchy) // 스포너가 켜져 있을 때만 <- (이 스크립트 붙어 있는 오브젝트가 켜져있다면)
        {
            _respawnCoroutines[pointIdx] = StartCoroutine(CoSpawnPointRoutine(info));
        }
    }

#if UNITY_EDITOR
    // 에디터에서 스폰 구역을 시각적으로 확인하기 위한 기즈모
    private void OnDrawGizmos()
    {
        // 스포너 본체의 위치
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one);

        // 자식 스폰 포인트들의 위치 
        Gizmos.color = new Color(1, 0, 0, 0.5f);

        int index = 0;
        foreach (Transform child in transform)
        {
            Gizmos.DrawWireSphere(child.position, 0.5f);

            // 본체와 스폰 포인트를 선으로 연결하여 소속을 명확히 함
            Gizmos.DrawLine(transform.position, child.position);

            // 씬 뷰에 인덱스 텍스트를 띄워 JSON의 pointIndex와 쉽게 매칭하도록 함
            UnityEditor.Handles.Label(child.position + Vector3.up * 1.5f, $"[{_spawnerId}] Point {index}");
            index++;
        }
    }
#endif
}
