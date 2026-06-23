using UnityEngine;

public static class GameLog
{
    // Log/LogWarning 호출은 에디터 또는 ENABLE_GAME_LOG 정의 빌드에서만 컴파일됨.
    // 그 외 릴리즈 빌드에서는 호출 자체(인자 평가 포함)가 제거되어 로그 오버헤드가 없음.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("ENABLE_GAME_LOG")]
    public static void Log(object message) => Debug.Log(message);

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("ENABLE_GAME_LOG")]
    public static void LogWarning(object message) => Debug.LogWarning(message);

    // 에러는 릴리즈 빌드에서도 진단을 위해 항상 출력.
    public static void LogError(object message) => Debug.LogError(message);
}
