using System;

// 측정 하네스 실행 인자 (계획서 0-5) — 빌드를 다시 하지 않고 정책을 바꿔 돌리기 위해.
//   OperationKivotos.exe -harness -policy Distance -shadows 0 -screen-width 1920 -screen-height 1080
//   -harness           : GameScene이 준비되면 하네스 자동 시작, 끝나면 종료
//   -policy <이름|1~7> : 실제 정책 (GameScene 인스펙터 값 대신). 이름은 ActivationPolicyType, 번호는 계획서 ①~⑦
//   -shadows <0|1>     : 그림자 정책 (비용 측정은 0)
//  인자가 없으면 전부 null/false — 인스펙터 값 그대로(에디터 플레이와 같음).
public static class HarnessArgs
{
    private static bool _parsed;
    private static bool _harness;
    private static ActivationPolicyType? _policy;
    private static bool? _shadows;

    public static bool Harness { get { Parse(); return _harness; } }
    public static ActivationPolicyType? Policy { get { Parse(); return _policy; } }
    public static bool? Shadows { get { Parse(); return _shadows; } }

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
                    else if (Enum.TryParse(next, true, out ActivationPolicyType parsed))
                        _policy = parsed;
                    else
                        GameLog.LogWarning($"[Harness] -policy '{next}' 해석 실패 — 인스펙터 값 사용");
                    break;

                case "-shadows":
                    if (next != null) _shadows = next != "0";
                    break;
            }
        }
    }
}
