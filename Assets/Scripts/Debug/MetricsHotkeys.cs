#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// [측정 계측] 개발 빌드/에디터에서 측정 체크포인트를 키로 마킹한다.
//  빌드에는 에디터 디버그 창(ResourceDebugWindow)이 없으므로, 그 창의 체크포인트 버튼을
//  대체하는 런타임 핫키다. 씬에 배치할 필요 없이 부팅 시 자동 생성되며,
//  릴리즈 빌드에선 클래스 전체가 컴파일에서 빠진다(#if 가드).
//
//   F5 ① 진입 직후 / F6 ② 팝업 첫 오픈 / F7 ③ 재오픈 / F8 ④ 던전 왕복 / F9 ⑤ 종료(+로드 통계)
//
//  각 키 → Checkpoint(구분선) + 버킷 핸들 리포트([ResourceReport]) + 런타임 메모리 총량([ResourceMemory]).
//  전부 GameLog 경유라 MetricsLogs/metrics_*.log 파일에 자동 기록된다.
//  ⚠ 파일 기록 전제: 빌드에 ENABLE_GAME_LOG 심볼 정의(없으면 GameLog.Log가 컴파일에서 제거됨).
public class MetricsHotkeys : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[MetricsHotkeys]");
        DontDestroyOnLoad(go);
        go.AddComponent<MetricsHotkeys>();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return; // 키보드 미연결(콘솔/모바일 등)

        if (kb.f5Key.wasPressedThisFrame) Mark("1. GameScene 진입 직후");
        else if (kb.f6Key.wasPressedThisFrame) Mark("2. 팝업 5종 첫 오픈 시작");
        else if (kb.f7Key.wasPressedThisFrame) Mark("3. 같은 팝업 재오픈 시작 (캐시 히트 비교)");
        else if (kb.f8Key.wasPressedThisFrame) Mark("4. Boss/Normal 던전 왕복 시작");
        else if (kb.f9Key.wasPressedThisFrame)
        {
            ResourceMetrics.LogLoadStats(); // 키별 로드 횟수·시간 덤프
            Mark("5. 측정 종료");
        }
    }

    // 체크포인트 1회 = 단계 마킹 + 버킷 핸들 수 + 런타임 메모리 총량을 한 번에 기록.
    private static void Mark(string label)
    {
        ResourceMetrics.Checkpoint(label);

        var rm = Managers.Resource;
        if (rm != null) rm.LogAliveReport(label); // [ResourceReport] 버킷 핸들 수 (빌드 유효)

        ResourceMetrics.LogRuntimeMemory(label);   // [ResourceMemory] 총 할당/텍스처 (빌드 유효 절대값)
    }
}
#endif
