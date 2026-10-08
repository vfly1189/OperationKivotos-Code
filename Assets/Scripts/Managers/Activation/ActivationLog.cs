using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// 켜고 끈 로그 (계획서 0-3) — 정책이 스포너를 켜거나 끈 순간을 한 줄씩 모았다가 씬 Clear · 플레이 종료 때 CSV로 쓴다.
//  프레임 중엔 메모리 목록에 쌓기만 한다(Debug.Log · 파일 쓰기 없음) → 0-4 계측의 프레임 시간을 흐리지 않는다.
//  그림자를 켜면 7개 정책이 한 파일에 같은 프레임 번호로 쌓인다(policy 열 = 계획서 번호 1~7).
//  출력: MetricsLogs/activation_{실제 정책}[_shadow]_{시각}.csv
//    열: frame, time, policy, spawnerId, state(on/off), playerX, playerZ, distance(스포너 중심까지 x·z 거리)
//    '#' 줄 = 정책 설정 · 실제/그림자 · 프레임 범위 · 동등성 검사 요약 (pandas: read_csv(path, comment='#'))
//  에디터 · 개발 빌드에서만. 릴리즈 빌드에선 호출째(인자 평가 포함) 사라진다.
public static class ActivationLog
{
    private struct Entry
    {
        public int Frame;
        public float Time;
        public int Policy;
        public int SpawnerId;
        public bool On;
        public float PlayerX;
        public float PlayerZ;
        public float Distance;
    }

    private class CheckStat
    {
        public int Frames;
        public int Mismatches;
        public int FirstMismatchFrame = -1;
    }

    private static readonly List<Entry> _entries = new List<Entry>(8192);
    private static readonly Dictionary<int, Vector3> _centerById = new Dictionary<int, Vector3>();
    private static readonly List<string> _policyLines = new List<string>();
    private static readonly Dictionary<string, CheckStat> _checks = new Dictionary<string, CheckStat>();

    private static bool _active;
    private static string _realName;
    private static bool _shadows;
    private static int _spawnerCount;
    private static int _startFrame;
    private static System.DateTime _startedAt;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Begin(SpawnerRecord[] records, bool shadows, System.DateTime startedAt)
    {
        Save();   // 이전 세션이 안 닫혔으면 먼저 쓴다

        _active = true;
        _realName = null;
        _shadows = shadows;
        _spawnerCount = records.Length;
        _startFrame = UnityEngine.Time.frameCount;
        _startedAt = startedAt;

        _centerById.Clear();
        foreach (SpawnerRecord record in records) _centerById[record._spawnerId] = record._center;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void AddPolicy(ActivationPolicyType type, ActivationPolicy policy, bool isReal)
    {
        if (!_active) return;
        if (isReal) _realName = type.ToString();
        _policyLines.Add($"# {(int)type} {ActivationPolicies.Label(type)} {(isReal ? "실제" : "그림자")} · {policy}");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Record(ActivationPolicyType type, int spawnerId, bool on, Vector3 playerPos)
    {
        if (!_active) return;

        float distance = -1f;
        if (_centerById.TryGetValue(spawnerId, out Vector3 c))
        {
            float dx = c.x - playerPos.x;
            float dz = c.z - playerPos.z;
            distance = Mathf.Sqrt(dx * dx + dz * dz);
        }

        _entries.Add(new Entry
        {
            Frame = UnityEngine.Time.frameCount,
            Time = UnityEngine.Time.time,
            Policy = (int)type,
            SpawnerId = spawnerId,
            On = on,
            PlayerX = playerPos.x,
            PlayerZ = playerPos.z,
            Distance = distance,
        });
    }

    // 동등성 검사 1프레임 결과 (계획서 2-5). 요약만 CSV 머리에 남는다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Check(string name, bool equal)
    {
        if (!_active) return;

        if (!_checks.TryGetValue(name, out CheckStat stat)) _checks[name] = stat = new CheckStat();
        stat.Frames++;
        if (equal) return;
        stat.Mismatches++;
        if (stat.FirstMismatchFrame < 0) stat.FirstMismatchFrame = UnityEngine.Time.frameCount;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Save()
    {
        if (!_active) return;

        // ResourceMetrics.LogDirectory와 같은 폴더. 그쪽은 #if로 릴리즈에서 빠지므로 직접 계산한다
        //  ([Conditional]은 호출만 지우고 이 본문은 릴리즈에서도 컴파일된다).
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "MetricsLogs"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"activation_{_realName}{(_shadows ? "_shadow" : "")}_{_startedAt:yyyyMMdd_HHmmss}.csv");

        int endFrame = UnityEngine.Time.frameCount;
        CultureInfo inv = CultureInfo.InvariantCulture;
        using (var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine($"# started: {_startedAt:yyyy-MM-dd HH:mm:ss} · spawners: {_spawnerCount} · frames: {_startFrame}~{endFrame} · entries: {_entries.Count}");
            foreach (string line in _policyLines) writer.WriteLine(line);
            foreach (KeyValuePair<string, CheckStat> kv in _checks)
                writer.WriteLine($"# check {kv.Key}: 불일치 {kv.Value.Mismatches} / {kv.Value.Frames} 프레임" +
                                 (kv.Value.FirstMismatchFrame >= 0 ? $" · 첫 불일치 #{kv.Value.FirstMismatchFrame}" : ""));
            writer.WriteLine("frame,time,policy,spawnerId,state,playerX,playerZ,distance");

            foreach (Entry e in _entries)
            {
                writer.WriteLine(string.Join(",",
                    e.Frame.ToString(inv),
                    e.Time.ToString("F3", inv),
                    e.Policy.ToString(inv),
                    e.SpawnerId.ToString(inv),
                    e.On ? "on" : "off",
                    e.PlayerX.ToString("F2", inv),
                    e.PlayerZ.ToString("F2", inv),
                    e.Distance.ToString("F2", inv)));
            }
        }

        foreach (KeyValuePair<string, CheckStat> kv in _checks)
            GameLog.Log($"[ActivationLog] 동등성 {kv.Key}: 불일치 {kv.Value.Mismatches} / {kv.Value.Frames} 프레임");
        GameLog.Log($"[ActivationLog] {_realName}{(_shadows ? " + 그림자 6" : "")} — {_entries.Count}줄 → {path}");

        _entries.Clear();
        _centerById.Clear();
        _policyLines.Clear();
        _checks.Clear();
        _active = false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 에디터에서 플레이를 멈추면 씬 Clear가 안 불릴 수 있다 → 종료 때 한 번 더 저장 시도.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitSaveOnQuit()
    {
        Application.quitting -= OnQuit;
        Application.quitting += OnQuit;
    }

    private static void OnQuit() => Save();
#endif
}
