using System.Collections.Generic;
using System.Threading; // [추가] CancellationToken 용
using UnityEngine;
using Cysharp.Threading.Tasks; // [추가] UniTask

public class MonsterSpawner : MonoBehaviour
{
    [Header("Spawner Info")]
    [SerializeField] private int _spawnerId;
    [SerializeField] private int _mapId = 1000; // [추가] 이 스포너가 속한 맵의 ID (예: 1000)

    // [변경 1] Coroutine 배열 대신 CancellationTokenSource(CTS) 배열 사용
    private CancellationTokenSource[] _respawnCts;
    private MonsterController[] _spawnedMonsters;
    private List<Transform> _spawnPoints = new List<Transform>();
    private List<MonsterController> _activeMonsters = new List<MonsterController>();

    private Dictionary<MonsterController, int> _monsterToPointIndex = new Dictionary<MonsterController, int>();
    private Dictionary<MonsterController, SpawnInfo> _monsterToSpawnInfo = new Dictionary<MonsterController, SpawnInfo>();

    private void Awake()
    {
        _spawnPoints.Clear();
        foreach (Transform child in transform)
            _spawnPoints.Add(child);

        int count = _spawnPoints.Count;
        _respawnCts = new CancellationTokenSource[count];
        _spawnedMonsters = new MonsterController[count];
    }

    public void SpawnMonsters()
    {
        SpawnerData data = Managers.Data.GetData<int, SpawnerData>(_spawnerId);

        if (data == null || data.spawnList == null || data.spawnList.Count == 0) return;

        foreach (var info in data.spawnList)
        {
            if (info.pointIndex >= _spawnPoints.Count) continue;

            // 이미 살아있거나 스폰 대기 중이면 무시
            if (_spawnedMonsters[info.pointIndex] != null || _respawnCts[info.pointIndex] != null) continue;

            // [변경 2] 새로운 CTS를 생성하고 UniTaskVoid 호출 (Fire and Forget)
            _respawnCts[info.pointIndex] = new CancellationTokenSource();
            SpawnPointRoutineAsync(info, _respawnCts[info.pointIndex].Token).Forget();
        }
    }

    public void DespawnMonsters()
    {
        if (this == null || !gameObject.activeInHierarchy) return;

        // [변경 3] 진행 중인 모든 스폰 타이머(UniTask)를 즉시 안전하게 취소!
        CancelAllSpawnTasks();

        foreach (var monster in _activeMonsters)
        {
            if (monster != null)
            {
                MonsterStat stat = monster.GetComponent<MonsterStat>();
                if (stat != null) stat.OnMonsterDead -= HandleMonsterDead;

                Managers.Resource.Destroy(monster.gameObject);
            }
        }

        _activeMonsters.Clear();
        _monsterToPointIndex.Clear();
        _monsterToSpawnInfo.Clear();

        for (int i = 0; i < _spawnedMonsters.Length; i++) _spawnedMonsters[i] = null;
    }

    // [추가] 모든 스폰 작업을 취소하는 헬퍼 함수
    private void CancelAllSpawnTasks()
    {
        for (int i = 0; i < _respawnCts.Length; i++)
        {
            if (_respawnCts[i] != null)
            {
                _respawnCts[i].Cancel();
                _respawnCts[i].Dispose();
                _respawnCts[i] = null;
            }
        }
    }

    private async UniTaskVoid SpawnPointRoutineAsync(SpawnInfo info, CancellationToken token)
    {
        int pointIdx = info.pointIndex;
        Transform spawnPoint = _spawnPoints[pointIdx];

        // 1. 몬스터 베이스 데이터 가져오기
        MonsterBaseData monsterBaseData = Managers.Data.GetData<int, MonsterBaseData>(info.monsterId);
        if (monsterBaseData == null)
        {
            Debug.LogError($"[MonsterSpawner] monsterId({info.monsterId}) 데이터 없음.");
            DisposeCts(pointIdx);
            return;
        }

        // 2. 맵 설정 데이터 가져오기
        MapMonsterConfig mapConfig = Managers.Data.GetData<int, MapMonsterConfig>(_mapId);
        if (mapConfig == null)
        {
            Debug.LogError($"[MonsterSpawner] 맵 정보({_mapId})가 없습니다.");
            DisposeCts(pointIdx);
            return;
        }

        // 3. 몬스터 등급에 따른 레벨 결정
        int spawnLevel = (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Elite)
            ? mapConfig.EliteMonsterLevel
            : mapConfig.NormalMonsterLevel;

        // 4. 레벨에 따른 스탯 배율 데이터 가져오기
        MonsterLevelByStat levelStat = Managers.Data.GetData<int, MonsterLevelByStat>(spawnLevel);

        try
        {
            if (info.delay > 0)
            {
                bool isCanceled = await UniTask.Delay(
                    System.TimeSpan.FromSeconds(info.delay),
                    cancellationToken: token
                ).SuppressCancellationThrow();

                if (isCanceled) return;
            }

            GameObject monsterObj = Managers.Resource.Instantiate(
                monsterBaseData.AddressableKey,
                spawnPoint.position,
                spawnPoint.rotation
            );

            if (monsterObj == null) return;

            MonsterController monsterCtrl = monsterObj.GetComponent<MonsterController>();
            MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();

            if (monsterStat == null || monsterCtrl == null)
            {
                Managers.Resource.Destroy(monsterObj);
                return;
            }

            // [핵심 변경] BaseData와 LevelStat을 함께 넘겨서 스탯을 계산하게 함
            monsterStat.Init(monsterBaseData, levelStat);

            _monsterToPointIndex[monsterCtrl] = pointIdx;
            _monsterToSpawnInfo[monsterCtrl] = info;

            monsterStat.OnMonsterDead -= HandleMonsterDead;
            monsterStat.OnMonsterDead += HandleMonsterDead;

            _spawnedMonsters[pointIdx] = monsterCtrl;
            _activeMonsters.Add(monsterCtrl);
        }
        finally
        {
            DisposeCts(pointIdx);
        }
    }
    private void DisposeCts(int pointIdx)
    {
        if (_respawnCts[pointIdx] != null)
        {
            _respawnCts[pointIdx].Dispose();
            _respawnCts[pointIdx] = null;
        }
    }

    private void HandleMonsterDead(MonsterController deadMonster)
    {
        // deadMonster가 딱 누군지 이미 알고 있으므로 바로 조회
        if (deadMonster == null) return;
        if (!_monsterToPointIndex.ContainsKey(deadMonster)) return; // 이미 처리됐거나 Despawn된 경우 방어

        int pointIdx = _monsterToPointIndex[deadMonster];
        SpawnInfo info = _monsterToSpawnInfo[deadMonster];

        _activeMonsters.Remove(deadMonster);
        _spawnedMonsters[pointIdx] = null;
        _monsterToPointIndex.Remove(deadMonster);
        _monsterToSpawnInfo.Remove(deadMonster);

        MonsterStat stat = deadMonster.GetComponent<MonsterStat>();
        if (stat != null) stat.OnMonsterDead -= HandleMonsterDead;

        if (gameObject.activeInHierarchy)
        {
            deadMonster.OnDespawned += () => StartRespawn(pointIdx, info);
        }
    }
    private void StartRespawn(int pointIdx, SpawnInfo info)
    {
        // 섹터가 이미 비활성화됐을 수 있으므로 재확인
        if (!gameObject.activeInHierarchy) return;

        _respawnCts[pointIdx] = new CancellationTokenSource();
        SpawnPointRoutineAsync(info, _respawnCts[pointIdx].Token).Forget();
    }


    private void OnDestroy()
    {
        // 스포너 자체가 파괴될 때 메모리 릭을 방지하기 위해 모든 태스크 강제 종료
        CancelAllSpawnTasks();
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
