using System;
using System.Collections.Generic;

// 끄기 실험(갱신 LOD 0단계) — 몬스터 비용 항목을 통째로 꺼서 "이 항목이 0이면 프레임이 최대 얼마나 줄어드나"(상한)를 잰다.
//  실제 게임 동작이 아니라 측정 전용. 필드 스포너 몹에만, 스폰 직후 SpawnerManager가 적용하고 다음 생애 OnSpawn이 되돌린다.
[Flags]
public enum MonsterCostOff
{
    None = 0,
    BehaviorTree = 1 << 0,   // 행동 트리 평가 건너뜀 — 판단이 멈추므로 이동 · 애니 전환 · 사격도 같이 줄어든다
    Animator = 1 << 1,       // Animator.enabled = false — 애니 이벤트 흉내는 SS 이관 때 없앴다(구 RangedMonsterController).
                             //  지금 켜면 끝 신호가 안 와 공격 패턴이 끝나지 않는다. 10-13 측정용으로만 남은 항목.
    HpBar = 1 << 2,          // HP바 오브젝트를 끔 — 스크립트 갱신 + 캔버스 갱신
    Avoidance = 1 << 3,      // NavMeshAgent 회피만 끔(이동은 유지)
}

// 측정 하네스 실행 인자 (계획서 0-5) — 빌드를 다시 하지 않고 정책을 바꿔 돌리기 위해.
//   OperationKivotos.exe -harness -policy Distance -shadows 0 -screen-width 1920 -screen-height 1080
//   -harness           : GameScene이 준비되면 하네스 자동 시작, 끝나면 종료
//   -policy <이름|1~7> : 실제 정책 (GameScene 인스펙터 값 대신). 이름은 ActivationPolicyType, 번호는 계획서 ①~⑦
//   -shadows <0|1>     : 그림자 정책 (비용 측정은 0)
//   -density <1~16>    : 밀도 배율 — 스포너당 마릿수 ×N (GameScene 인스펙터 값 대신). 스포너 수 · 위치는 그대로
//   -off <항목[+항목]> : 끄기 실험 — bt · anim · hpbar · avoid (none = 끄지 않음). 여러 개는 + 또는 쉼표로
//   -lod <0|1>         : 갱신 LOD (화면 밖 · 비교전 몹 덜 돌리기). GameScene 인스펙터 값 대신
//  인자가 없으면 전부 null/false — 인스펙터 값 그대로(에디터 플레이와 같음).
public static class HarnessArgs
{
    private static bool _parsed;
    private static bool _harness;
    private static ActivationPolicyType? _policy;
    private static bool? _shadows;
    private static int? _density;
    private static MonsterCostOff? _costOff;
    private static bool? _updateLod;

    public static bool Harness { get { Parse(); return _harness; } }
    public static ActivationPolicyType? Policy { get { Parse(); return _policy; } }
    public static bool? Shadows { get { Parse(); return _shadows; } }
    public static int? Density { get { Parse(); return _density; } }

    public static MonsterCostOff? CostOff { get { Parse(); return _costOff; } }
    public static bool? UpdateLod { get { Parse(); return _updateLod; } }

    public const int MaxDensity = 16;

    // 파일 이름 · 인자에 쓰는 짧은 이름. 순서 = 파일 이름 꼬리표 순서
    private static readonly (MonsterCostOff Flag, string Token)[] CostOffTokens =
    {
        (MonsterCostOff.BehaviorTree, "bt"),
        (MonsterCostOff.Animator, "anim"),
        (MonsterCostOff.HpBar, "hpbar"),
        (MonsterCostOff.Avoidance, "avoid"),
    };

    // "bt+anim" — None이면 빈 문자열
    public static string CostOffTag(MonsterCostOff off)
    {
        List<string> parts = new List<string>();
        foreach (var (flag, token) in CostOffTokens)
            if ((off & flag) != 0) parts.Add(token);
        return string.Join("+", parts);
    }

    private static void Parse()
    {
        if (_parsed) return;
        _parsed = true;

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string next = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i].ToLowerInvariant())
            {
                case "-harness":
                    _harness = true;
                    break;

                case "-policy":
                    if (next == null) break;
                    if (int.TryParse(next, out int number) && Enum.IsDefined(typeof(ActivationPolicyType), number))
                        _policy = (ActivationPolicyType)number;
                    // 쉼표는 거부 — Enum.TryParse가 플래그로 합쳐 버린다("Sector,Distance" = 1|5 = Distance, 10-10 실제로 겪음)
                    else if (!next.Contains(",") && Enum.TryParse(next, true, out ActivationPolicyType parsed))
                        _policy = parsed;
                    else
                        GameLog.LogWarning($"[Harness] -policy '{next}' 해석 실패 — 인스펙터 값 사용");
                    break;

                case "-shadows":
                    if (next != null) _shadows = next != "0";
                    break;

                case "-density":
                    if (next == null) break;
                    if (int.TryParse(next, out int density) && density >= 1 && density <= MaxDensity)
                        _density = density;
                    else
                        GameLog.LogWarning($"[Harness] -density '{next}' 해석 실패(1~{MaxDensity}) — 인스펙터 값 사용");
                    break;

                case "-off":
                    if (next == null) break;
                    _costOff = ParseCostOff(next);
                    break;

                case "-lod":
                    if (next != null) _updateLod = next != "0";
                    break;
            }
        }
    }

    // 모르는 이름은 경고만 하고 무시 — 나머지 항목은 그대로 적용
    private static MonsterCostOff ParseCostOff(string text)
    {
        MonsterCostOff off = MonsterCostOff.None;
        foreach (string raw in text.Split('+', ','))
        {
            string token = raw.Trim().ToLowerInvariant();
            if (token.Length == 0 || token == "none") continue;

            bool known = false;
            foreach (var (flag, name) in CostOffTokens)
                if (token == name) { off |= flag; known = true; }
            if (!known) GameLog.LogWarning($"[Harness] -off '{token}' 모르는 항목 — 무시 (가능: bt · anim · hpbar · avoid)");
        }
        return off;
    }
}
