using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [Header("Spawner Info")]
    [SerializeField] private int _spawnerId;

    public int SpawnerID => _spawnerId;

    //[SerializeField] private int _mapId = 1006;

    //private CancellationTokenSource[] _respawnCts;
    //private BaseMonsterController[] _spawnedMonsters;
    //private List<Transform> _spawnPoints = new List<Transform>();
    //private List<BaseMonsterController> _activeMonsters = new List<BaseMonsterController>();

    //private Dictionary<BaseMonsterController, int> _monsterToPointIndex = new Dictionary<BaseMonsterController, int>();
    //private Dictionary<BaseMonsterController, SpawnInfo> _monsterToSpawnInfo = new Dictionary<BaseMonsterController, SpawnInfo>();


    #region Legacy
    //public void SpawnMonsters()
    //{
    //    SpawnerData data = Managers.Data.GetData<int, SpawnerData>(_spawnerId);


    //    foreach (var info in data.spawnList)
    //    {
    //        if (info.pointIndex >= _spawnPoints.Count) continue;

    //        // 이미 살아있거나 스폰 대기 중이면 무시
    //        if (_spawnedMonsters[info.pointIndex] != null || _respawnCts[info.pointIndex] != null) continue;

    //        _respawnCts[info.pointIndex] = new CancellationTokenSource();
    //        SpawnPointRoutineAsync(info, _respawnCts[info.pointIndex].Token).Forget();
    //    }
    //}

    //public void DespawnMonsters()
    //{
    //    if (this == null || !gameObject.activeInHierarchy) return;

    //    CancelAllSpawnTasks();

    //    foreach (var monster in _activeMonsters)
    //    {
    //        if (monster != null)
    //        {
    //            MonsterStat stat = monster.GetComponent<MonsterStat>();
    //            if (stat != null) stat.OnMonsterDead -= HandleMonsterDead;

    //            Managers.Resource.Destroy(monster.gameObject);
    //        }
    //    }

    //    _activeMonsters.Clear();
    //    _monsterToPointIndex.Clear();
    //    _monsterToSpawnInfo.Clear();

    //    for (int i = 0; i < _spawnedMonsters.Length; i++) _spawnedMonsters[i] = null;
    //}

    //private void CancelAllSpawnTasks()
    //{
    //    for (int i = 0; i < _respawnCts.Length; i++)
    //    {
    //        if (_respawnCts[i] != null)
    //        {
    //            _respawnCts[i].Cancel();
    //            _respawnCts[i].Dispose();
    //            _respawnCts[i] = null;
    //        }
    //    }
    //}

    //private async UniTaskVoid SpawnPointRoutineAsync(SpawnInfo info, CancellationToken token)
    //{
    //    int pointIdx = info.pointIndex;
    //    Transform spawnPoint = _spawnPoints[pointIdx];

    //    //  몬스터 BaseData만 가져오기 (AddressableKey를 얻기 위함)
    //    MonsterBaseData monsterBaseData = Managers.Data.GetData<int, MonsterBaseData>(info.monsterId);
    //    if (monsterBaseData == null)
    //    {
    //        GameLog.LogError($"[MonsterSpawner] monsterId({info.monsterId}) 데이터 없음.");
    //        DisposeCts(pointIdx);
    //        return;
    //    }

    //    try
    //    {
    //        // 스폰 딜레이 대기
    //        if (info.delay > 0)
    //        {
    //            bool isCanceled = await UniTask.Delay(
    //                System.TimeSpan.FromSeconds(info.delay),
    //                cancellationToken: token
    //            ).SuppressCancellationThrow();

    //            if (isCanceled) return;
    //        }

    //        //  모든 생성 로직과 데이터 주입을 Factory에 위임
    //        GameObject monsterObj = await MonsterFactory.CreateMonsterByMonsterIDAsync(
    //            info.monsterId,
    //            _mapId,
    //            spawnPoint,
    //            token
    //        );

    //        if (monsterObj == null) return;

    //        BaseMonsterController monsterCtrl = monsterObj.GetComponent<BaseMonsterController>();
    //        MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();

    //        if (monsterStat == null || monsterCtrl == null)
    //        {
    //            Managers.Resource.Destroy(monsterObj);
    //            return;
    //        }

    //        // 딕셔너리 및 상태 캐싱
    //        _monsterToPointIndex[monsterCtrl] = pointIdx;
    //        _monsterToSpawnInfo[monsterCtrl] = info;

    //        // 사망 이벤트 구독
    //        monsterStat.OnMonsterDead -= HandleMonsterDead;
    //        monsterStat.OnMonsterDead += HandleMonsterDead;

    //        _spawnedMonsters[pointIdx] = monsterCtrl;
    //        _activeMonsters.Add(monsterCtrl);
    //    }
    //    finally
    //    {
    //        DisposeCts(pointIdx);
    //    }
    //}

    //private void DisposeCts(int pointIdx)
    //{
    //    if (_respawnCts[pointIdx] != null)
    //    {
    //        _respawnCts[pointIdx].Dispose();
    //        _respawnCts[pointIdx] = null;
    //    }
    //}

    //private void HandleMonsterDead(BaseMonsterController deadMonster)
    //{
    //    if (deadMonster == null) return;
    //    if (!_monsterToPointIndex.ContainsKey(deadMonster)) return;

    //    int pointIdx = _monsterToPointIndex[deadMonster];
    //    SpawnInfo info = _monsterToSpawnInfo[deadMonster];

    //    _activeMonsters.Remove(deadMonster);
    //    _spawnedMonsters[pointIdx] = null;
    //    _monsterToPointIndex.Remove(deadMonster);
    //    _monsterToSpawnInfo.Remove(deadMonster);

    //    MonsterStat stat = deadMonster.GetComponent<MonsterStat>();
    //    if (stat != null) stat.OnMonsterDead -= HandleMonsterDead;

    //    if (gameObject.activeInHierarchy)
    //    {
    //        // 디스폰 애니메이션까지 끝난 뒤에 리스폰 타이머를 돌림
    //        deadMonster.OnDespawned += () => StartRespawn(pointIdx, info);
    //    }
    //}

    //private void StartRespawn(int pointIdx, SpawnInfo info)
    //{
    //    if (!gameObject.activeInHierarchy) return;

    //    _respawnCts[pointIdx] = new CancellationTokenSource();
    //    SpawnPointRoutineAsync(info, _respawnCts[pointIdx].Token).Forget();
    //}

    #endregion

    public SpawnerRecord Build(SpawnerData data)
    {
        if (data == null)
        {
            GameLog.LogWarning($"[Spawner] 표에 없는 spawnerId {_spawnerId} ({name})");
            return null;
        }

        // 데이터 오류 하나로 씬 로딩 전체가 멈추지 않게, 잘못된 항목은 경고 후 건너뛴다 (S5).
        int childCount = transform.childCount;
        HashSet<int> usedPoints = new HashSet<int>();
        List<SpawnSlot> slots = new List<SpawnSlot>(data.spawnList.Count);
        foreach(SpawnInfo info in data.spawnList)
        {
            if (info.pointIndex < 0 || info.pointIndex >= childCount)
            {
                GameLog.LogWarning($"[Spawner] {_spawnerId} ({name}) pointIndex {info.pointIndex} 범위 밖 (자식 {childCount}개) — 건너뜀");
                continue;
            }
            if (!usedPoints.Add(info.pointIndex))
            {
                GameLog.LogWarning($"[Spawner] {_spawnerId} ({name}) pointIndex {info.pointIndex} 중복 지정 — 한 포인트 2마리 방지로 건너뜀");
                continue;
            }

            Transform p = transform.GetChild(info.pointIndex);
            slots.Add(new SpawnSlot(_spawnerId, info.monsterId, info.delay, p.position, p.rotation));
        }

        if (usedPoints.Count < childCount)
            GameLog.LogWarning($"[Spawner] {_spawnerId} ({name}) 자식 포인트 {childCount - usedPoints.Count}개가 표에서 쓰이지 않음");

        return new SpawnerRecord(_spawnerId, transform.position, slots.ToArray());
    }


#if UNITY_EDITOR
    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.cyan;
    //    Gizmos.DrawWireCube(transform.position, Vector3.one);
    //    Gizmos.color = new Color(1, 0, 0, 0.5f);

    //    int index = 0;
    //    foreach (Transform child in transform)
    //    {
    //        Gizmos.DrawWireSphere(child.position, 0.5f);
    //        Gizmos.DrawLine(transform.position, child.position);
    //        UnityEditor.Handles.Label(child.position + Vector3.up * 1.5f, $"[{_spawnerId}] Point {index}");
    //        index++;
    //    }
    //}
#endif
}