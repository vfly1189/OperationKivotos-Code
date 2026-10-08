using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class SpawnerManager
{
    readonly Dictionary<int, SpawnerRecord> _records = new Dictionary<int, SpawnerRecord>();
    Dictionary<BaseMonsterController, SpawnSlot> _monsterBySpawnSlot = new Dictionary<BaseMonsterController, SpawnSlot>();

    CancellationTokenSource _sceneCTS;
    public int _currentMapID;
    public int _spawnBudgetPerFrame = int.MaxValue;

    // 리스폰 간격 지터 (S6) — respawnTime × (1 ± 이 값). 같은 시각에 죽은 몹이 같은 프레임에 몰려 리스폰하지 않게.
    //  난수는 맵 ID로 시드 → 같은 경로를 다시 돌면 같은 값이 나와 측정 재현성이 유지된다.
    public float _respawnJitter = 0.2f;
    System.Random _rng;

    // 교전 유지 (계획서 0-2) — 스포너가 꺼져도 교전 중인 몹은 회수하지 않고 유예 목록에 둔다.
    //  슬롯은 Alive 그대로라 스포너가 다시 켜져도 중복 스폰이 없다. 매 틱 상태를 봐서 교전이 끝나면 회수한다
    //  — 귀환 이벤트만 보면 "집 2m 안에서 싸우다 대상 놓침"(귀환 없이 Idle)이 영원히 남는다.
    //  Before 녹화(BenchmarkTester F3)에선 리쉬와 함께 끈다. 리쉬 없이 켜 두면 교전이 끝나지 않을 수 있다.
    public bool _keepEngaged = true;
    readonly Dictionary<SpawnSlot, SpawnerRecord> _deferred = new Dictionary<SpawnSlot, SpawnerRecord>();
    readonly List<SpawnSlot> _deferredDone = new List<SpawnSlot>();
    // 누수 검증용 — 플레이어가 떠난 뒤 귀환 시간 안에 0이 되어야 한다. 상한 = 슬롯 수.
    //  로그 대신 계측 CSV(frames의 deferred 열 · events의 defer/recall)로 본다 — Debug.Log가 틱 비용에 섞이므로.
    public int DeferredCount => _deferred.Count;

    // 등록(Init)과 가동(Run)을 나눈다. 가동 전에도 Activate는 받아 두고, 스폰만 미룬다.
    //  몬스터 OnSpawn이 파티를 읽으므로 파티가 준비된 뒤(GameScene.CreateCharacters 이후)에 가동.
    bool _isRunning;

    public void Init(int mapID, List<SpawnerRecord> records)
    {
        _sceneCTS = new CancellationTokenSource();

        _currentMapID = mapID;
        _rng = new System.Random(mapID);

        foreach(SpawnerRecord record in records)
        {
            _records[record._spawnerId] = record;
        }
    }

    public void Run() => _isRunning = true;
    public bool IsRunning => _isRunning;

    // 이 스포너 몹 중 교전 중인 수 — 하네스가 끌기 전에 어그로를 확인한다(계획서 0-5 ⓑ).
    public int CountEngaged(int spawnerId)
    {
        if (!_records.TryGetValue(spawnerId, out SpawnerRecord record)) return 0;

        int count = 0;
        foreach (SpawnSlot slot in record._slots)
            if (slot._state == SlotState.Alive && slot._instance != null && slot._instance.IsEngaged) count++;
        return count;
    }

    public void Activate(int spawnerID)
    {
        if (!_records.TryGetValue(spawnerID, out SpawnerRecord record)) return;
        if (record.IsActive) return;

        // 꺼진 동안 리스폰 기한이 지난 슬롯은 켜는 순간 나타난다 → 리스폰이 아니라 정책 때문에 생긴 등장 (계측 구분)
        float now = Time.time;
        foreach (SpawnSlot slot in record._slots)
            if (slot._state == SlotState.Waiting && slot._readyAt <= now) slot._afterDeath = false;

        record.IsActive = true;          // 스위치만 켠다. 스폰은 틱이 한다.
    }

    public void DeActivate(int spawnerID)
    {
        if (!_records.TryGetValue(spawnerID, out SpawnerRecord record)) return;
        record.IsActive = false;

        float now = Time.time;

        foreach (SpawnSlot slot in record._slots)
        {
            switch (slot._state)
            {
                case SlotState.Spawning:
                    slot._gen++;                       // 날아가는 주문 무효화
                    slot._state = SlotState.Waiting;
                    slot._readyAt = now;
                    break;

                case SlotState.Alive:
                    if (slot._instance == null) { RecoverLost(slot, now); break; }   // 이미 밖에서 파괴됨

                    if (_keepEngaged && slot._instance.IsEngaged)       // 교전 중 — 끝나면 틱이 회수
                    {
                        _deferred[slot] = record;
                        FieldMetrics.Event(FieldMetrics.EventKind.Defer, slot, slot._instance.transform.position, "engaged", true);
                        break;
                    }

                    Recall(slot, now, "policy");
                    break;

                    // Waiting · Dying: 그대로
            }
        }
    }

    async UniTaskVoid SpawnAsync(SpawnSlot slot, int gen)
    {
        GameObject monster = await MonsterFactory.CreateMonsterByMonsterIDAsync(
            slot._monsterId,
            _currentMapID,
            slot._pos,
            slot._rot,
            _sceneCTS.Token
        );
        // 여기까지 요청하고 받을때까지 시간이 걸릴 수 있음 그 사이에 active deactive가 반복돼서 gen 이 바뀌면 회수해야됨.
        if(slot._gen != gen)
        {
            if (monster != null) Managers.Resource.Destroy(monster);
            return;
        }
        //만약 같다면
        if(monster == null)
        {
            // 실패(데이터 누락 · 로드 실패) — 바로 다시 요청하면 매 프레임 재시도하므로 리스폰 간격만큼 물러난다.
            slot._state = SlotState.Waiting;
            slot._readyAt = NextReadyAt(slot, Time.time);
            return;
        }

        
        slot._instance = monster.GetComponent<BaseMonsterController>();
        slot._state = SlotState.Alive;
        if (slot._instance is NormalMonsterController normal) normal.EnableLeash();   // 리쉬는 필드 몹 규칙
        slot._instance.Stat.OnMonsterDead += HandleDead;
        slot._instance.OnDespawned += HandleDespawned;

        _monsterBySpawnSlot.Add(slot._instance, slot);

        FieldMetrics.Event(FieldMetrics.EventKind.Spawn, slot, monster.transform.position,
                           slot._afterDeath ? "respawn" : "activate", false);
        slot._afterDeath = false;
    }

    public void OnUpdate()
    {
        if (!_isRunning) return;

        using (FieldMetrics.SpawnerTick.Auto())
        {
            float now = Time.time;

            // 스폰 예산이 바닥나면 SpawnDue가 중간에 끝나므로 유예 목록을 먼저 본다.
            TickDeferred(now);
            SpawnDue(now);
        }
    }

    void SpawnDue(float now)
    {
        int budget = _spawnBudgetPerFrame;                  // 프레임당 스폰 요청 수 (나중에 조정)

        foreach (SpawnerRecord record in _records.Values)
        {
            if (!record.IsActive) continue;

            foreach (SpawnSlot slot in record._slots)
            {
                if (budget <= 0) return;

                // 회수 경로(Resource.Destroy) 밖에서 파괴된 몬스터 — 슬롯이 Alive/Dying으로 굳지 않게 복구.
                if ((slot._state == SlotState.Alive || slot._state == SlotState.Dying) && slot._instance == null)
                    RecoverLost(slot, now);

                if (slot._state != SlotState.Waiting || now < slot._readyAt) continue;

                slot._gen++;
                slot._state = SlotState.Spawning;
                SpawnAsync(slot, slot._gen).Forget();
                budget--;
            }
        }
    }

    // 유예 목록에서 빠지는 경우 셋: 스포너가 다시 켜짐(그대로 산다) · 이미 Alive가 아님(사망 경로가 처리) · 교전 끝(회수).
    void TickDeferred(float now)
    {
        if (_deferred.Count == 0) return;

        foreach (KeyValuePair<SpawnSlot, SpawnerRecord> pair in _deferred)
        {
            SpawnSlot slot = pair.Key;

            if (pair.Value.IsActive || slot._state != SlotState.Alive) { _deferredDone.Add(slot); continue; }

            if (slot._instance == null) RecoverLost(slot, now);
            else if (_keepEngaged && slot._instance.IsEngaged) continue;
            else Recall(slot, now, "deferred");

            _deferredDone.Add(slot);
        }

        foreach (SpawnSlot slot in _deferredDone) _deferred.Remove(slot);
        _deferredDone.Clear();
    }

    // 살아 있는 몹을 정책 때문에 회수한다. 리스폰 간격 없이 바로 Waiting — 다시 켜지면 즉시 스폰.
    //  cause — policy(스포너가 꺼진 순간) | deferred(교전 유예가 끝난 뒤). 계측용.
    void Recall(SpawnSlot slot, float now, string cause)
    {
        using (FieldMetrics.Recall.Auto())
        {
            GameObject go = slot._instance.gameObject;
            FieldMetrics.Event(FieldMetrics.EventKind.Recall, slot, go.transform.position, cause, slot._instance.IsEngaged);

            Detach(slot);                      // 구독 해제 · 딕셔너리 제거가 회수보다 먼저
            Managers.Resource.Destroy(go);
            slot._state = SlotState.Waiting;
            slot._readyAt = now;
            slot._afterDeath = false;
        }
    }

    // 계측 표본 (0-4) — 직전 프레임 한 줄. Managers.Update 맨 앞(이번 프레임 정책 · 스폰 전)에서 불러
    //  몹 수 · 화면 안 몹 수 · 유예 수 · 켜진 스포너 수가 직전 프레임 끝 상태가 된다. 화면 판정 비용은 Field.Metrics 마커로 따로 잡힌다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void SampleMetrics()
    {
        if (!FieldMetrics.IsActive) return;

        using (FieldMetrics.Sample.Auto())
        {
            Camera cam = Camera.main;   // 표본마다 한 번 — 몹마다 부르면 비용이 몹 수에 비례해 커진다
            int onScreen = 0;
            foreach (BaseMonsterController mc in _monsterBySpawnSlot.Keys)
            {
                if (mc == null) continue;
                FieldMetrics.Viewport(cam, mc.transform.position, out bool visible);
                if (visible) onScreen++;
            }

            int activeSpawners = 0;
            foreach (SpawnerRecord record in _records.Values)
                if (record.IsActive) activeSpawners++;

            FieldMetrics.SampleFrame(_monsterBySpawnSlot.Count, onScreen, _deferred.Count, activeSpawners);
        }
    }

    // 씬 이탈 시 (GameScene.Clear). 날아가는 스폰은 취소되고, 남은 몬스터는 씬 풀 정리가 회수한다.
    public void Clear()
    {
        _isRunning = false;

        _sceneCTS?.Cancel();
        _sceneCTS?.Dispose();
        _sceneCTS = null;

        _records.Clear();
        _monsterBySpawnSlot.Clear();
        _deferred.Clear();
    }

    void HandleDead(BaseMonsterController mc)
    {
        if (!_monsterBySpawnSlot.TryGetValue(mc, out SpawnSlot slot)) return;

        slot._state = SlotState.Dying;
    }

    void HandleDespawned(BaseMonsterController mc)
    {
        if (!_monsterBySpawnSlot.TryGetValue(mc, out SpawnSlot slot)) return;

        Detach(slot);                                  // 회수(Resource.Destroy)는 몬스터가 직접 한다
        slot._state = SlotState.Waiting;
        slot._afterDeath = true;
        slot._readyAt = NextReadyAt(slot, Time.time);
    }

    // 슬롯에서 몬스터를 떼어 낸다. 풀 재사용 시 같은 인스턴스가 다른 슬롯에 다시 등록되므로 반드시 제거.
    void Detach(SpawnSlot slot)
    {
        BaseMonsterController mc = slot._instance;
        mc.Stat.OnMonsterDead -= HandleDead;
        mc.OnDespawned -= HandleDespawned;
        _monsterBySpawnSlot.Remove(mc);
        slot._instance = null;
    }

    // 파괴된 인스턴스는 구독 해제(Stat 접근)가 불가능하므로 연결만 끊는다. 이벤트는 오브젝트와 함께 사라진다.
    //  Dictionary 키 비교는 InstanceID라 파괴된 참조로도 Remove된다.
    void RecoverLost(SpawnSlot slot, float now)
    {
        GameLog.LogWarning($"[Spawner] 회수 경로 밖에서 파괴된 몬스터 (monster {slot._monsterId}, {slot._state}) — 슬롯 복구");
        _monsterBySpawnSlot.Remove(slot._instance);
        slot._instance = null;
        slot._state = SlotState.Waiting;
        slot._readyAt = NextReadyAt(slot, now);
    }

    float NextReadyAt(SpawnSlot slot, float now)
    {
        float jitter = 1f + _respawnJitter * (float)(_rng.NextDouble() * 2.0 - 1.0);
        return now + slot._respawnTime * jitter;
    }
}
