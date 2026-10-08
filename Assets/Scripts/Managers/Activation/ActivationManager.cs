using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct ActivationLayout
{
    public Sector[] _sectors;
    public SpawnerRecord[] _records;
}

public class ActivationManager
{
    // 정책 하나 + 그 정책이 지난 프레임에 켠 집합. 그림자도 자기 집합을 따로 들어야
    //  히스테리시스(⑤⑥⑦)가 남의 상태가 아니라 자기 상태로 판정한다.
    private class Run
    {
        public ActivationPolicyType Type;
        public ActivationPolicy Policy;
        public HashSet<int> Current = new HashSet<int>();
        public HashSet<int> Next = new HashSet<int>();
    }

    // [0] = 실제 정책(스포너를 켜고 끔). [1..] = 그림자(계산 · 로그만). 그림자 판정은 실제 몬스터와 무관하다
    //  — 정책은 플레이어 위치와 자기 직전 집합만 본다 → 한 번의 플레이로 7개를 같은 경로에서 비교한다.
    private readonly List<Run> _runs = new List<Run>();

    // 동등성 검사 대상 (계획서 2-5) — 없으면 null
    private Run _distance;            // ⑤
    private Run _spotlightDistance;   // ⑥ = ⑤ (같은 프레임)
    private Run _cullingGroup;        // ⑦ = ⑤ (1프레임 전)

    private const int MismatchWarnLimit = 10;
    private int _warned56;
    private int _warned57;

    // 기즈모용 — 마지막 판정 위치와 스포너 목록
    private SpawnerRecord[] _records;
    private Vector3 _playerPos;
    private bool _hasPlayerPos;

    // 전체 켜기(BenchmarkTester F1) — 스포너는 전부 켜 두고, 정책은 그대로 판정만 한다(집합 · 로그 · 동등성 유지).
    //  풀면 실제 정책 집합 밖의 스포너만 끈다 → 바로 정책 상태로 돌아간다.
    private bool _forceAll;

    // shadows = true면 나머지 6개를 그림자로 같이 돌린다. 비용 측정(Phase 3) 때는 꺼야 한다 — 그림자 선택 비용이 프레임 시간에 섞인다.
    public void Init(ActivationLayout layout, ActivationPolicyType real, bool shadows)
    {
        Clear();

        AddRun(real, layout);
        if (shadows)
        {
            foreach (ActivationPolicyType type in System.Enum.GetValues(typeof(ActivationPolicyType)))
                if (type != real) AddRun(type, layout);
        }

        _distance = Find(ActivationPolicyType.Distance);
        _spotlightDistance = Find(ActivationPolicyType.SpotlightDistance);
        _cullingGroup = Find(ActivationPolicyType.CullingGroup);

        _records = layout._records;
        System.DateTime startedAt = System.DateTime.Now;   // 켜고 끈 로그와 계측 파일이 같은 시각 이름을 쓴다
        ActivationLog.Begin(layout._records, shadows, startedAt);
        FieldMetrics.Begin(real, shadows, startedAt);
        foreach (Run run in _runs) ActivationLog.AddPolicy(run.Type, run.Policy, run == _runs[0]);
    }

    private void AddRun(ActivationPolicyType type, ActivationLayout layout)
    {
        ActivationPolicy policy = ActivationPolicies.Create(type);
        policy.Init(layout);   // 그림자도 인스턴스가 따로다(칸 캐시 · CullingGroup을 각자 쥔다)
        _runs.Add(new Run { Type = type, Policy = policy });
    }

    private Run Find(ActivationPolicyType type) => _runs.Find(r => r.Type == type);

    public void OnUpdate()
    {
        if (_runs.Count == 0) return;

        // 파티 구성 전(첫 로딩 중)에는 판정하지 않는다. 구성되는 프레임부터 자연히 맞춰진다.
        List<BaseCharacter> members = Managers.Party?.GetMember();
        if (members == null || members.Count == 0) return;

        BaseCharacter current = Managers.Party.GetCurrentCharacter();
        if (current == null) return;

        Vector3 position = current.transform.position;
        _playerPos = position;
        _hasPlayerPos = true;

        for (int i = 0; i < _runs.Count; i++) Step(_runs[i], position, isReal: i == 0, apply: i == 0 && !_forceAll);

        CheckEquivalence();
    }

    // apply = 스포너를 실제로 켜고 끈다(실제 정책, 전체 켜기 중이 아닐 때). 선택 비용은 실제/그림자 마커로 나눠 잰다.
    private static void Step(Run run, Vector3 position, bool isReal, bool apply)
    {
        run.Next.Clear();
        using ((isReal ? FieldMetrics.Select : FieldMetrics.ShadowSelect).Auto())
            run.Policy.SelectSpawners(position, run.Current, run.Next);

        foreach (int id in run.Next)
        {
            if (run.Current.Contains(id)) continue;
            if (apply) Managers.Spawner.Activate(id);
            ActivationLog.Record(run.Type, id, true, position);
        }
        foreach (int id in run.Current)
        {
            if (run.Next.Contains(id)) continue;
            if (apply) Managers.Spawner.DeActivate(id);
            ActivationLog.Record(run.Type, id, false, position);
        }

        (run.Current, run.Next) = (run.Next, run.Current);   // 할당 없이 교체 → Next에 직전 프레임 집합이 남는다
    }

    // ⑤ = ⑥ 은 같은 프레임, ⑦ = ⑤ 는 ⑦이 1프레임 늦으므로 ⑤의 직전 프레임 집합(교체 후 Next)과 비교한다.
    private void CheckEquivalence()
    {
        if (_distance == null) return;

        if (_spotlightDistance != null)
            Check("⑤=⑥", _distance.Current, _spotlightDistance.Current, ref _warned56);
        if (_cullingGroup != null)
            Check("⑦=⑤(1프레임 전)", _distance.Next, _cullingGroup.Current, ref _warned57);
    }

    private static void Check(string name, HashSet<int> expected, HashSet<int> actual, ref int warned)
    {
        bool equal = expected.SetEquals(actual);
        ActivationLog.Check(name, equal);
        if (equal || warned >= MismatchWarnLimit) return;

        warned++;
        // 불일치 때만 차이를 만든다(할당은 이때만)
        GameLog.LogWarning($"[ActivationShadow] {name} 불일치 #{Time.frameCount} — " +
                           $"빠짐 [{string.Join(",", expected.Except(actual))}] · 더함 [{string.Join(",", actual.Except(expected))}]" +
                           (warned == MismatchWarnLimit ? " (이후 경고 생략, 개수는 CSV 요약에)" : ""));
    }

    public void SetForceAll(bool on)
    {
        if (_forceAll == on || _records == null) return;
        _forceAll = on;

        HashSet<int> real = _runs[0].Current;
        foreach (SpawnerRecord record in _records)
        {
            if (on) Managers.Spawner.Activate(record._spawnerId);
            else if (!real.Contains(record._spawnerId)) Managers.Spawner.DeActivate(record._spawnerId);
        }
    }

    public void Clear()
    {
        foreach (Run run in _runs) run.Policy.Clear();
        _runs.Clear();
        _distance = _spotlightDistance = _cullingGroup = null;
        _warned56 = _warned57 = 0;

        ActivationLog.Save();
        FieldMetrics.Save();
        _records = null;
        _hasPlayerPos = false;
        _forceAll = false;
    }

#if UNITY_EDITOR
    // Managers.OnDrawGizmos가 부른다. 실제 정책의 범위 + 스포너 켜짐(초록 구) / 꺼짐(회색 테두리). 그림자는 안 그린다.
    public void DrawGizmos()
    {
        if (_runs.Count == 0 || !_hasPlayerPos) return;

        Run real = _runs[0];
        real.Policy.DrawGizmos(_playerPos);

        if (_records == null) return;
        foreach (SpawnerRecord record in _records)
            ActivationGizmos.DrawSpawner(record._center, real.Current.Contains(record._spawnerId));
    }
#endif
}
