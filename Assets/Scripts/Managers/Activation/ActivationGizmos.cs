#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 활성화 정책 기즈모 공용. 판정이 바닥(x·z)이라 전부 플레이어 높이의 수평면에 그린다.
public static class ActivationGizmos
{
    public static readonly Color On = new Color(0.2f, 1f, 0.3f);        // 켜기 반경 · 켜진 스포너
    public static readonly Color Off = new Color(1f, 0.55f, 0.1f);      // 끄기 반경
    public static readonly Color Window = new Color(0.3f, 0.8f, 1f);    // 스포트라이트 창 · 인접 섹터
    public static readonly Color Standing = new Color(1f, 0.9f, 0.2f);  // 서 있는 섹터 · 플레이어 칸
    public static readonly Color Idle = new Color(0.5f, 0.5f, 0.5f);    // 꺼진 스포너

    // ⑤ · ⑥ · ⑦ — 켜기 원 안에 들어오면 켜고, 끄기 원 밖으로 나가면 끈다. 두 원 사이는 이전 상태 유지.
    public static void DrawRange(Vector3 playerPos, float onRadius, float offRadius)
    {
        DrawCircle(playerPos, onRadius, On, $"켜기 {onRadius}m");
        DrawCircle(playerPos, offRadius, Off, $"끄기 {offRadius}m");
    }

    public static void DrawCircle(Vector3 center, float radius, Color color, string label)
    {
        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.Label(center + Vector3.right * radius, label);
    }

    // ③ · ④ · ⑥ — 창 안 칸 하나. 플레이어 칸은 채워서 구분한다.
    public static void DrawCell(float minX, float minZ, float size, float y, bool isPlayerCell)
    {
        Vector3 center = new Vector3(minX + size * 0.5f, y, minZ + size * 0.5f);
        Vector3 extent = new Vector3(size, 0f, size);

        if (isPlayerCell)
        {
            Gizmos.color = new Color(Standing.r, Standing.g, Standing.b, 0.15f);
            Gizmos.DrawCube(center, extent);
        }
        Gizmos.color = isPlayerCell ? Standing : new Color(Window.r, Window.g, Window.b, 0.5f);
        Gizmos.DrawWireCube(center, extent);
    }

    // 창 전체 외곽 — 이 사각형 안에 중심이 있는 스포너가 후보.
    public static void DrawWindow(float minX, float minZ, float width, float y)
    {
        Gizmos.color = Window;
        Gizmos.DrawWireCube(new Vector3(minX + width * 0.5f, y, minZ + width * 0.5f), new Vector3(width, 0f, width));
    }

    public static void DrawSpawner(Vector3 center, bool active)
    {
        Vector3 p = center + Vector3.up;
        Gizmos.color = active ? On : Idle;
        if (active) Gizmos.DrawSphere(p, 1f);
        else Gizmos.DrawWireSphere(p, 1f);
    }
}
#endif
