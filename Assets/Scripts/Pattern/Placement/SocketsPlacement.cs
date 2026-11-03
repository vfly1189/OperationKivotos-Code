using System.Collections.Generic;
using UnityEngine;

// AnchorSet의 소켓 여러 개를 그대로 점으로 — 키를 넣은 순서 = 실행 순서 (예: Spawn_1, Spawn_2, Spawn_3)
// 기준점이 여러 개라 _anchor는 쓰지 않는다.
public class SocketsPlacement : Placement
{
    [SerializeField] private string[] _keys;

    public override void Collect(in PatternContext ctx, List<Pose> results)
    {
        if (ctx.sockets == null)
        {
            Debug.LogWarning($"[SocketsPlacement] {name}: 시전자에 AnchorSet 없음");
            return;
        }

        foreach (string key in _keys)
        {
            if (ctx.sockets.TryGet(key, out Transform t) && t != null)
                results.Add(new Pose(t.position, Quaternion.Euler(0f, t.eulerAngles.y, 0f)));
            else
                Debug.LogWarning($"[SocketsPlacement] {name}: 소켓 '{key}' 없음");
        }
    }

    // 소켓 위치는 실행 중에만 알 수 있어 미리보기는 그리지 않는다
    protected override void Generate(Pose origin, List<Pose> results) { }
}
