using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

// =========================================================================
// [Phase 0.5 계측] 비용 지표(②) 측정기
//  - UI 오픈: 요청→표시 시간 + 이후 관찰 창의 최악 프레임(히치)
//  - 씬 전환: 전환 구간 메모리 피크 (신·구 리소스 공존 비용 — P3 전후 비교용)
//  - 로드 통계: 키별 실제 로드 횟수·시간 (After에서 "같은 짐 다시 꺼낸 횟수"의 근거)
//  - 아틀라스 전개: 전량 스프라이트 캐싱의 메인 스레드 비용
//
//  동작 무변경 원칙: 관찰 + 로그만. 모든 진입점은 [Conditional]로
//  에디터/개발 빌드 전용 — 릴리즈 빌드에선 호출 자체가 컴파일에서 제거된다.
//  로그 태그: [UIMetric] / [TransitionMetric] / [LoadStats] / [AtlasMetric]
//  (측정 지표 체계는 docs/ResourceSystem/Refactor_Plan.md · Metrics_BeforeAfter.pdf 참고)
// =========================================================================
public static class ResourceMetrics
{
    // ---------------------------------------------------------------------
    // 1) UI 오픈 측정 — 요청 시점부터 FrameWindow 프레임 동안 프레임 타임 관찰.
    //    팝업 내부 콘텐츠(아틀라스/스탠딩 등)는 표시 "이후" 프레임에 로드되므로
    //    표시 마킹 후에도 관찰을 이어가 그 히치까지 잡는다.
    // ---------------------------------------------------------------------
    private const int FrameWindow = 60; // 요청 후 관찰 프레임 수 (~1초)

    private class UIOpenSession
    {
        public string Key;
        public float StartRealtime;
        public float ShownRealtime = -1f;
    }

    private static UIOpenSession _uiSession;

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void BeginUIOpen(string key)
    {
        // 팝업은 UIManager._isLoadingPopup으로 동시 오픈이 막혀 있어 세션 1개로 충분
        var session = new UIOpenSession { Key = key, StartRealtime = Time.realtimeSinceStartup };
        _uiSession = session;
        SampleUIOpenAsync(session).Forget();
    }

    // "화면에 표시됨" 시점 마킹 (요청→표시 = 오픈 레이턴시)
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void MarkUIShown(string key)
    {
        if (_uiSession != null && _uiSession.Key == key && _uiSession.ShownRealtime < 0f)
            _uiSession.ShownRealtime = Time.realtimeSinceStartup;
    }

    private static async UniTaskVoid SampleUIOpenAsync(UIOpenSession session)
    {
        float maxDt = 0f, sumDt = 0f;
        int maxFrame = -1;

        for (int i = 0; i < FrameWindow; i++)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            float dt = Time.unscaledDeltaTime;
            sumDt += dt;
            if (dt > maxDt) { maxDt = dt; maxFrame = i; }
        }

        string latency = session.ShownRealtime >= 0f
            ? $"{(session.ShownRealtime - session.StartRealtime) * 1000f:F1}ms"
            : "(표시 마킹 없음 — 로드 실패?)";

        GameLog.Log($"[UIMetric] {session.Key} — 요청→표시 {latency} | " +
                    $"요청 후 {FrameWindow}프레임: 최악 {maxDt * 1000f:F1}ms(#{maxFrame}) 평균 {sumDt / FrameWindow * 1000f:F1}ms");

        if (_uiSession == session) _uiSession = null;
    }

    // ---------------------------------------------------------------------
    // 2) 씬 전환 메모리 피크 — 전환 시작(SceneManagerEx)부터
    //    다음 씬 활성화(LoadingScene 종료)까지 매 프레임 샘플링.
    // ---------------------------------------------------------------------
    private static bool _transitionSampling;

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void BeginSceneTransition(string toScene)
    {
        if (_transitionSampling) return;
        _transitionSampling = true;
        SampleTransitionAsync(toScene).Forget();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void EndSceneTransition()
    {
        _transitionSampling = false;
    }

    private static async UniTaskVoid SampleTransitionAsync(string toScene)
    {
        const int SafetyFrameCap = 3600; // ~60초. End 미호출로 전환이 비정상 종료돼도 샘플러는 멈춘다

        long SampleNow() => Profiler.GetTotalAllocatedMemoryLong() + Profiler.GetAllocatedMemoryForGraphicsDriver();

        long start = SampleNow();
        long peak = start;
        int peakFrame = 0;
        float t0 = Time.realtimeSinceStartup;
        int frame = 0;

        while (_transitionSampling && frame < SafetyFrameCap)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            frame++;
            long cur = SampleNow();
            if (cur > peak) { peak = cur; peakFrame = frame; }
        }

        long end = SampleNow();
        GameLog.Log($"[TransitionMetric] → {toScene}: 피크 {ToMB(peak)}(#{peakFrame}프레임) | " +
                    $"시작 {ToMB(start)} → 종료 {ToMB(end)} | {frame}프레임 {Time.realtimeSinceStartup - t0:F1}s" +
                    (frame >= SafetyFrameCap ? " ⚠ 안전 상한 도달 — EndSceneTransition 미호출 의심" : ""));
        _transitionSampling = false;
    }

    // ---------------------------------------------------------------------
    // 3) 키별 실제 로드 횟수·시간 — 캐시 히트는 제외, Addressables 로드가
    //    실제 발생한 것만 기록. 디버그 창 [로드 통계] 버튼으로 덤프.
    // ---------------------------------------------------------------------
    private struct LoadStat { public int Count; public long TotalMs; public long MaxMs; }

    private static readonly Dictionary<string, LoadStat> _loadStats = new Dictionary<string, LoadStat>();

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void RecordLoad(string key, long elapsedMs)
    {
        _loadStats.TryGetValue(key, out var s);
        s.Count++;
        s.TotalMs += elapsedMs;
        if (elapsedMs > s.MaxMs) s.MaxMs = elapsedMs;
        _loadStats[key] = s;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogLoadStats()
    {
        if (_loadStats.Count == 0)
        {
            GameLog.Log("[LoadStats] 기록 없음");
            return;
        }

        int totalLoads = 0, reloadedKeys = 0;
        long totalMs = 0;
        foreach (var s in _loadStats.Values)
        {
            totalLoads += s.Count;
            totalMs += s.TotalMs;
            if (s.Count > 1) reloadedKeys++;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[LoadStats] 키 {_loadStats.Count}개 / 실제 로드 {totalLoads}회 / 합계 {totalMs}ms / 재로드 발생 키 {reloadedKeys}개");
        sb.AppendLine("  ---- 로드 시간 상위 20 (횟수 | 합계 | 최대 | 키) ----");
        foreach (var kv in _loadStats.OrderByDescending(kv => kv.Value.TotalMs).Take(20))
            sb.AppendLine($"  {kv.Value.Count,3}회 | {kv.Value.TotalMs,5}ms | max {kv.Value.MaxMs,4}ms | {kv.Key}");

        if (reloadedKeys > 0)
        {
            sb.AppendLine("  ---- 재로드(2회 이상) 발생 키 — lazy화 비용의 직접 증거 ----");
            foreach (var kv in _loadStats.Where(kv => kv.Value.Count > 1).OrderByDescending(kv => kv.Value.Count))
                sb.AppendLine($"  {kv.Value.Count,3}회 | {kv.Key}");
        }

        GameLog.Log(sb.ToString());
    }

    // ---------------------------------------------------------------------
    // 4) 아틀라스 전량 전개 비용 — GetSprites + 캐시 등록은 메인 스레드에서
    //    돌아가는 히치 후보. Phase 0.5 해체 전후 비교의 직접 근거.
    // ---------------------------------------------------------------------
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void RecordAtlasExpansion(string atlasKey, int spriteCount, long elapsedMs)
    {
        GameLog.Log($"[AtlasMetric] {atlasKey} 전개 — 스프라이트 {spriteCount}개 캐싱, {elapsedMs}ms (메인 스레드)");
    }

    private static string ToMB(long bytes) => $"{bytes / 1048576f:F1}MB";

    // ---------------------------------------------------------------------
    // 5) 측정 로그 자동 파일 기록 — 콘솔 복사 불필요
    //    측정 태그가 붙은 로그만 걸러서 <프로젝트 루트>/MetricsLogs/metrics_*.log 에
    //    실시간 append. 디버그 창의 체크포인트 버튼으로 시나리오 단계를 마킹한다.
    // ---------------------------------------------------------------------

    // 시나리오 단계 구분선 (디버그 창 체크포인트 버튼 → 파일에 기록됨)
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Checkpoint(string label)
    {
        GameLog.Log($"[Checkpoint] ━━━━━ {label} ━━━━━");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly string[] CaptureTags =
    {
        "[UIMetric]", "[TransitionMetric]", "[AtlasMetric]", "[LoadStats]", "[Checkpoint]",
        "[ResourceReport]", "[ResourceMemory]", "[ResourceLeak]", "[Preload]",
        // 스코프 부재 등 리소스 계층의 에러. 없으면 "로그에 에러가 없다"를 근거로 쓸 수 없다
        // (Phase 3b 검증에서 실제로 판단 불가 상황이 발생했음).
        "[Resource]",
    };

    private static System.IO.StreamWriter _logWriter;

    public static string LogDirectory =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "MetricsLogs"));

    public static string CurrentLogFilePath { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitLogCapture()
    {
        // 도메인 리로드 비활성 환경에서도 중복 구독 방지
        Application.logMessageReceived -= OnLogMessage;
        Application.logMessageReceived += OnLogMessage;
        Application.quitting -= CloseLogFile;
        Application.quitting += CloseLogFile;
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        for (int i = 0; i < CaptureTags.Length; i++)
        {
            if (condition.Contains(CaptureTags[i]))
            {
                Write(condition);
                return;
            }
        }
    }

    private static void Write(string message)
    {
        if (_logWriter == null)
        {
            System.IO.Directory.CreateDirectory(LogDirectory);
            CurrentLogFilePath = System.IO.Path.Combine(
                LogDirectory, $"metrics_{System.DateTime.Now:yyyyMMdd_HHmmss}.log");
            _logWriter = new System.IO.StreamWriter(CurrentLogFilePath, append: false, System.Text.Encoding.UTF8)
            {
                AutoFlush = true, // 에디터 크래시에도 기록 보존
            };
            _logWriter.WriteLine($"# 측정 세션 시작 — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _logWriter.WriteLine($"# 태그 필터: {string.Join(" ", CaptureTags)}");
            _logWriter.WriteLine();
        }

        _logWriter.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    private static void CloseLogFile()
    {
        if (_logWriter == null) return;
        _logWriter.WriteLine();
        _logWriter.WriteLine($"# 측정 세션 종료 — {System.DateTime.Now:HH:mm:ss}");
        _logWriter.Dispose();
        _logWriter = null;
    }
#endif
}
