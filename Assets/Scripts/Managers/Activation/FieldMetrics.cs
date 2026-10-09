using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

// 존재 판정 비교 계측 (계획서 0-4) — 3절 측정 항목을 한 세션(씬 진입 ~ Clear)마다 CSV 두 개로 쓴다.
//  ActivationLog와 같은 원칙: 프레임 중엔 메모리 목록에 쌓기만 하고(로그 · 파일 쓰기 없음) 씬 Clear · 종료 때 쓴다.
//  출력: MetricsLogs/field_{실제 정책}[_shadow]_{시각}_frames.csv · _events.csv (켜고 끈 로그 activation_*.csv와 같은 시각)
//
//  frames — 프레임마다 한 줄. 시간 열은 ProfilerRecorder가 모은 그 프레임의 마커 합계(같은 마커 여러 번 = 합).
//    frame, dtMs(그 프레임 길이), mainMs(메인 스레드), selectUs(실제 정책 선택), shadowUs(그림자 선택),
//    tickUs(스포너 틱), popUs · statUs · onSpawnUs · activateUs(스폰 4단계), recallUs(정책 회수), metricsUs(이 계측 자체),
//    spawns · recalls · defers(그 프레임 수), live(스포너 몹 · 죽는 중 포함) · onScreen · deferred · activeSpawners(다음 프레임 시작 시점), memMB
//  events — 스폰 · 정책 회수 · 교전 유예 한 건씩. 화면 판정(onScreen)은 그 순간 몹 발밑 점의 카메라 뷰포트.
//    frame, time, kind(spawn|recall|defer|segment), spawnerId, monsterId, cause, x, z, vx, vy, depth, onScreen, engaged
//    cause — spawn: activate(정책이 켬 · 처음) | respawn(사망 후 리스폰, 정책과 무관) · recall: policy(바로) | deferred(교전 끝난 뒤)
//            segment: 하네스 구간 이름(그 프레임부터 그 구간, x · z = 플레이어 위치)
//  '#' 줄 = 설정 · 프레임 상한 · 풀 통계(시작 크기 · 최대 사용 · 확장 횟수). pandas: read_csv(path, comment='#')
//
//  주의: targetFrameRate · vSync가 걸려 있으면 dtMs가 상한에 붙는다. 비용 비교(Phase 3)는 상한을 풀고 잰다(머리 줄에 기록).
public static class FieldMetrics
{
    // 마커는 릴리즈에서도 남는다(ENABLE_PROFILER가 없으면 비용 0). 프로파일러 창에서도 같은 이름으로 보인다.
    public static readonly ProfilerMarker Select = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Activation.Select");
    public static readonly ProfilerMarker ShadowSelect = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Activation.Shadow");
    public static readonly ProfilerMarker SpawnerTick = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Spawner.Tick");
    public static readonly ProfilerMarker SpawnPop = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Spawn.PoolPop");
    // 풀 꺼내기 안쪽 분해(Phase 1-3 원인 확인) — PoolPop 안에 중첩. 나머지(PoolPop − 셋)는 GetComponent · 이름 문자열 · 사전 · HashSet.
    //  같은 Pool.Pop · Instantiate를 HP바 · 투사체 · VFX도 지나므로(교전 중 매 발사) 몬스터 스폰의 꺼내기 안에서만 센다 → PopSub.
    public static readonly ProfilerMarker PopFindScene = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Spawn.PoolPop.FindScene");
    public static readonly ProfilerMarker PopSetParent = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Spawn.PoolPop.SetParent");
    public static readonly ProfilerMarker PopSetPose = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Spawn.PoolPop.SetPose");
    public static readonly ProfilerMarker SpawnStat = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Spawn.SetStat");
    public static readonly ProfilerMarker SpawnOnSpawn = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Spawn.OnSpawn");
    public static readonly ProfilerMarker SpawnActivate = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Spawn.SetActive");
    public static readonly ProfilerMarker Recall = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Recall");
    public static readonly ProfilerMarker Sample = new ProfilerMarker(ProfilerCategory.Scripts, "Field.Metrics");

    private static bool _inMonsterPop;

    // 몬스터 스폰의 풀 꺼내기 구간(PoolPop 마커) — 이 안에서만 PopSub가 센다.
    public static MonsterPopScope MonsterPop() => new MonsterPopScope(SpawnPop);
    public static SubScope PopSub(ProfilerMarker marker) => new SubScope(marker, _inMonsterPop);

    public readonly struct MonsterPopScope : System.IDisposable
    {
        private readonly ProfilerMarker _marker;
        public MonsterPopScope(ProfilerMarker marker) { _marker = marker; _marker.Begin(); _inMonsterPop = true; }
        public void Dispose() { _inMonsterPop = false; _marker.End(); }
    }

    public readonly struct SubScope : System.IDisposable
    {
        private readonly ProfilerMarker _marker;
        private readonly bool _on;
        public SubScope(ProfilerMarker marker, bool on) { _marker = marker; _on = on; if (on) marker.Begin(); }
        public void Dispose() { if (_on) _marker.End(); }
    }

    public enum EventKind { Spawn, Recall, Defer, Segment }

    private struct FrameRow
    {
        public int Frame;
        public float DtMs, MainMs;
        public float SelectUs, ShadowUs, TickUs, PopUs, StatUs, OnSpawnUs, ActivateUs, RecallUs, MetricsUs;
        public float PopFindUs, PopParentUs, PopPoseUs;
        public int Spawns, Recalls, Defers;
        public int Live, OnScreen, Deferred, ActiveSpawners;
        public float MemMB;
    }

    private struct EventRow
    {
        public int Frame;
        public float Time;
        public EventKind Kind;
        public int SpawnerId, MonsterId;
        public string Cause;
        public float X, Z, Vx, Vy, Depth;
        public bool OnScreen, Engaged;
    }

    private class PoolStat
    {
        public int Initial, Peak, Grows;
    }

    // 프레임 기록은 고정 크기 묶음(청크)으로 쌓는다. List 하나에 쌓으면 용량을 두 배로 늘릴 때 기존 줄을 전부 복사해
    //  그 프레임에 수 ms가 튄다(정책 7개 측정 10-09: 계측 자체 4.8~6.7ms가 거리형의 최악 프레임이었다).
    //  묶음은 Begin에서 예상치만큼 미리 만들어 세션 사이에 재사용 → 측정 중엔 할당도 복사도 없다. 넘치면 묶음 하나만 추가.
    private const int ChunkSize = 8192;
    private const int PreallocChunks = 32;   // 262,144프레임 — 상한 없는 하네스 1회(약 20만) + 여유. 한 줄 92B라 약 24MB
    private static readonly List<FrameRow[]> _chunks = new List<FrameRow[]>(PreallocChunks);
    private static int _frameCount;
    private static readonly List<EventRow> _events = new List<EventRow>(16384);   // 하네스 1회 최대 약 6천 줄(①) — 확장 없음
    private static readonly Dictionary<string, PoolStat> _pools = new Dictionary<string, PoolStat>();

    // 그 프레임에 일어난 수 — 프레임 번호 홀짝으로 두 칸. 표본은 다음 프레임에 뜨므로 현재 프레임 칸을 건드리지 않는다.
    private static readonly int[] _spawns = new int[2];
    private static readonly int[] _recalls = new int[2];
    private static readonly int[] _defers = new int[2];

    private static bool _active;
    private static string _name;
    private static System.DateTime _startedAt;
    private static int _startFrame;
    private static int _lastSampledFrame;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static ProfilerRecorder _main, _select, _shadow, _tick, _pop, _stat, _onSpawn, _activate, _recall, _sample;
    private static ProfilerRecorder _popFind, _popParent, _popPose;
#endif

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Begin(ActivationPolicyType real, bool shadows, System.DateTime startedAt)
    {
        Save();   // 이전 세션이 안 닫혔으면 먼저 쓴다

        _active = true;
        _name = $"{real}{(shadows ? "_shadow" : "")}";
        _startedAt = startedAt;
        _startFrame = Time.frameCount;
        _lastSampledFrame = Time.frameCount - 1;
        System.Array.Clear(_spawns, 0, 2);
        System.Array.Clear(_recalls, 0, 2);
        System.Array.Clear(_defers, 0, 2);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _frameCount = 0;
        while (_chunks.Count < PreallocChunks) _chunks.Add(new FrameRow[ChunkSize]);
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        _select = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Activation.Select");
        _shadow = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Activation.Shadow");
        _tick = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Spawner.Tick");
        _pop = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Spawn.PoolPop");
        _stat = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Spawn.SetStat");
        _onSpawn = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Spawn.OnSpawn");
        _activate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Spawn.SetActive");
        _recall = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Recall");
        _sample = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Metrics");
        _popFind = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Spawn.PoolPop.FindScene");
        _popParent = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Spawn.PoolPop.SetParent");
        _popPose = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Field.Spawn.PoolPop.SetPose");
#endif
    }

    public static bool IsActive => _active;

    // 직전 프레임 한 줄. Managers.Update 맨 앞(= 그 프레임의 정책 · 스폰이 다 끝난 다음 프레임)에서 부른다.
    //  ProfilerRecorder.LastValue가 직전 프레임 값이고, unscaledDeltaTime이 직전 프레임 길이라 같은 프레임으로 맞는다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void SampleFrame(int live, int onScreen, int deferred, int activeSpawners)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_active) return;

        int frame = Time.frameCount - 1;
        if (frame <= _lastSampledFrame) return;   // 같은 프레임 두 번 방지
        _lastSampledFrame = frame;

        int p = frame & 1;
        AddFrame(new FrameRow
        {
            Frame = frame,
            DtMs = Time.unscaledDeltaTime * 1000f,
            MainMs = _main.LastValue * 1e-6f,
            SelectUs = _select.LastValue * 1e-3f,
            ShadowUs = _shadow.LastValue * 1e-3f,
            TickUs = _tick.LastValue * 1e-3f,
            PopUs = _pop.LastValue * 1e-3f,
            StatUs = _stat.LastValue * 1e-3f,
            OnSpawnUs = _onSpawn.LastValue * 1e-3f,
            ActivateUs = _activate.LastValue * 1e-3f,
            RecallUs = _recall.LastValue * 1e-3f,
            PopFindUs = _popFind.LastValue * 1e-3f,
            PopParentUs = _popParent.LastValue * 1e-3f,
            PopPoseUs = _popPose.LastValue * 1e-3f,
            MetricsUs = _sample.LastValue * 1e-3f,
            Spawns = _spawns[p],
            Recalls = _recalls[p],
            Defers = _defers[p],
            Live = live,
            OnScreen = onScreen,
            Deferred = deferred,
            ActiveSpawners = activeSpawners,
            MemMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
        });
        _spawns[p] = _recalls[p] = _defers[p] = 0;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static void AddFrame(in FrameRow row)
    {
        int chunk = _frameCount / ChunkSize;
        if (chunk == _chunks.Count) _chunks.Add(new FrameRow[ChunkSize]);   // 예상 초과 — 묶음 하나(약 0.75MB)만, 복사 없음
        _chunks[chunk][_frameCount % ChunkSize] = row;
        _frameCount++;
    }
#endif

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Event(EventKind kind, SpawnSlot slot, Vector3 position, string cause, bool engaged)
    {
        if (!_active) return;

        int p = Time.frameCount & 1;
        switch (kind)
        {
            case EventKind.Spawn: _spawns[p]++; break;
            case EventKind.Recall: _recalls[p]++; break;
            case EventKind.Defer: _defers[p]++; break;
        }

        Vector3 v = Viewport(Camera.main, position, out bool onScreen);
        _events.Add(new EventRow
        {
            Frame = Time.frameCount,
            Time = Time.time,
            Kind = kind,
            SpawnerId = slot._spawnerId,
            MonsterId = slot._monsterId,
            Cause = cause,
            X = position.x,
            Z = position.z,
            Vx = v.x,
            Vy = v.y,
            Depth = v.z,
            OnScreen = onScreen,
            Engaged = engaged,
        });
    }

    // 하네스 구간 시작 표시 (계획서 0-5) — 분석이 구간별로 나눈다. 프레임 수 집계에는 안 들어간다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Segment(string label, Vector3 playerPos)
    {
        if (!_active) return;

        _events.Add(new EventRow
        {
            Frame = Time.frameCount,
            Time = Time.time,
            Kind = EventKind.Segment,
            SpawnerId = -1,
            MonsterId = -1,
            Cause = label,
            X = playerPos.x,
            Z = playerPos.z,
        });
    }

    // 몹 발밑 점이 카메라 화면 안인가 (카메라 앞 · 뷰포트 0~1). 카메라가 없으면 화면 밖으로 본다.
    //  카메라는 호출부가 넘긴다 — 매 프레임 표본은 몹 수만큼 부르므로 Camera.main을 한 번만 읽게.
    public static Vector3 Viewport(Camera cam, Vector3 position, out bool onScreen)
    {
        if (cam == null) { onScreen = false; return new Vector3(-1f, -1f, -1f); }

        Vector3 v = cam.WorldToViewportPoint(position);
        onScreen = v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        return v;
    }

    // 풀 사용량 — Pool.Pop마다. 세션 동안의 최대 사용 수와 확장(스택이 비어 새로 만든) 횟수.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void PoolUsage(string poolName, int initial, int inUse, bool grew)
    {
        if (!_active) return;

        if (!_pools.TryGetValue(poolName, out PoolStat stat)) _pools[poolName] = stat = new PoolStat { Initial = initial };
        if (inUse > stat.Peak) stat.Peak = inUse;
        if (grew) stat.Grows++;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Save()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_active) return;

        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "MetricsLogs"));
        Directory.CreateDirectory(dir);
        string stem = Path.Combine(dir, $"field_{_name}_{_startedAt:yyyyMMdd_HHmmss}");
        CultureInfo inv = CultureInfo.InvariantCulture;

        string header = $"# started: {_startedAt:yyyy-MM-dd HH:mm:ss} · policy: {_name} · frames: {_startFrame}~{Time.frameCount}" +
                        $" · targetFrameRate: {Application.targetFrameRate} · vSyncCount: {QualitySettings.vSyncCount}" +
                        $" · build: {(Application.isEditor ? "editor" : Debug.isDebugBuild ? "development" : "release")}";

        using (var w = new StreamWriter(stem + "_frames.csv", false, System.Text.Encoding.UTF8))
        {
            w.WriteLine(header);
            foreach (KeyValuePair<string, PoolStat> kv in _pools)
                w.WriteLine($"# pool {kv.Key}: 시작 {kv.Value.Initial} · 최대 사용 {kv.Value.Peak} · 확장 {kv.Value.Grows}");
            w.WriteLine("frame,dtMs,mainMs,selectUs,shadowUs,tickUs,popUs,statUs,onSpawnUs,activateUs,recallUs,metricsUs," +
                        "spawns,recalls,defers,live,onScreen,deferred,activeSpawners,memMB,popFindUs,popParentUs,popPoseUs");
            for (int i = 0; i < _frameCount; i++)
            {
                FrameRow r = _chunks[i / ChunkSize][i % ChunkSize];
                w.WriteLine(string.Join(",",
                    r.Frame.ToString(inv),
                    r.DtMs.ToString("F3", inv), r.MainMs.ToString("F3", inv),
                    r.SelectUs.ToString("F1", inv), r.ShadowUs.ToString("F1", inv), r.TickUs.ToString("F1", inv),
                    r.PopUs.ToString("F1", inv), r.StatUs.ToString("F1", inv), r.OnSpawnUs.ToString("F1", inv),
                    r.ActivateUs.ToString("F1", inv), r.RecallUs.ToString("F1", inv), r.MetricsUs.ToString("F1", inv),
                    r.Spawns.ToString(inv), r.Recalls.ToString(inv), r.Defers.ToString(inv),
                    r.Live.ToString(inv), r.OnScreen.ToString(inv), r.Deferred.ToString(inv), r.ActiveSpawners.ToString(inv),
                    r.MemMB.ToString("F1", inv),
                    r.PopFindUs.ToString("F1", inv), r.PopParentUs.ToString("F1", inv), r.PopPoseUs.ToString("F1", inv)));
            }
        }

        using (var w = new StreamWriter(stem + "_events.csv", false, System.Text.Encoding.UTF8))
        {
            w.WriteLine(header);
            w.WriteLine("frame,time,kind,spawnerId,monsterId,cause,x,z,vx,vy,depth,onScreen,engaged");
            foreach (EventRow e in _events)
            {
                w.WriteLine(string.Join(",",
                    e.Frame.ToString(inv), e.Time.ToString("F3", inv),
                    e.Kind.ToString().ToLowerInvariant(),
                    e.SpawnerId.ToString(inv), e.MonsterId.ToString(inv), e.Cause,
                    e.X.ToString("F2", inv), e.Z.ToString("F2", inv),
                    e.Vx.ToString("F3", inv), e.Vy.ToString("F3", inv), e.Depth.ToString("F2", inv),
                    e.OnScreen ? "1" : "0", e.Engaged ? "1" : "0"));
            }
        }

        foreach (KeyValuePair<string, PoolStat> kv in _pools)
            if (kv.Value.Grows > 0) GameLog.LogWarning($"[FieldMetrics] 풀 확장 {kv.Key}: {kv.Value.Grows}회 (시작 {kv.Value.Initial} · 최대 사용 {kv.Value.Peak})");
        GameLog.Log($"[FieldMetrics] {_name} — 프레임 {_frameCount}줄 · 이벤트 {_events.Count}줄 → {stem}_*.csv");

        _main.Dispose(); _select.Dispose(); _shadow.Dispose(); _tick.Dispose(); _pop.Dispose();
        _stat.Dispose(); _onSpawn.Dispose(); _activate.Dispose(); _recall.Dispose(); _sample.Dispose();
        _popFind.Dispose(); _popParent.Dispose(); _popPose.Dispose();

        _frameCount = 0;   // 묶음은 남겨 다음 세션이 재사용
        _events.Clear();
        _pools.Clear();
        _active = false;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 에디터에서 플레이를 멈추면 씬 Clear가 안 불릴 수 있다 → 종료 때 한 번 더 저장 시도 (ActivationLog와 같음).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitSaveOnQuit()
    {
        Application.quitting -= OnQuit;
        Application.quitting += OnQuit;
    }

    private static void OnQuit() => Save();
#endif
}
