using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnchorSource { Caster, Target, Socket, Saved }
public enum FacingSource { AnchorForward, TowardTarget, World }
public enum PointFacing { Inherit, Outward, Inward }   // 모양이 만든 점 하나하나가 볼 쪽

// 기준점 하나 — 어디서(source) · 어느 쪽을 앞으로(facing) · 얼마나 떨어져(offset).
// Placement 안의 설정값. ctx만 읽는다 — 시전자가 누구인지(BMC) 모른다.
[Serializable]
public struct Anchor
{
    public AnchorSource source;
    public string key;           // Socket = AnchorSet 키, Saved = memo 키
    public Vector3 offset;       // facing 기준 로컬 오프셋 ("앞 3m" = (0,0,3))
    public FacingSource facing;

    public bool Resolve(in PatternContext ctx, out Pose pose)
    {
        pose = default;
        Vector3 pos;
        Quaternion rot;

        switch (source)
        {
            case AnchorSource.Caster:
                if (ctx.owner == null) return false;
                pos = ctx.owner.transform.position;
                rot = ctx.owner.transform.rotation;
                break;

            case AnchorSource.Target:
                Transform target = CurrentTarget(ctx);
                if (target == null) return false;               // 전멸 · 비활성 — 정상 상황이라 조용히 건너뜀
                pos = target.position;
                rot = target.rotation;
                break;

            case AnchorSource.Socket:
                if (ctx.sockets == null || !ctx.sockets.TryGet(key, out Transform socket) || socket == null)
                {
                    Debug.LogWarning($"[Anchor] 소켓 '{key}' 없음 — AnchorSet 확인");
                    return false;
                }
                pos = socket.position;
                rot = socket.rotation;
                break;

            case AnchorSource.Saved:
                if (ctx.memo == null || !ctx.memo.TryGetPoints(key, out List<Pose> saved))
                {
                    Debug.LogWarning($"[Anchor] 저장된 점 '{key}' 없음 — 앞선 Event의 saveAs 확인");
                    return false;
                }
                pos = saved[0].position;
                rot = saved[0].rotation;
                break;

            default:
                return false;
        }

        switch (facing)
        {
            case FacingSource.TowardTarget:
                // 시전자 → 타겟 방향. 타겟이 없거나 겹쳐 있으면 기준점 방향 그대로
                Transform t = CurrentTarget(ctx);
                if (t != null && ctx.owner != null)
                {
                    Vector3 dir = t.position - ctx.owner.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f) rot = Quaternion.LookRotation(dir);
                }
                break;

            case FacingSource.World:
                rot = Quaternion.identity;
                break;
        }

        rot = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);   // 바닥 패턴 — 기울기는 버리고 좌우 방향만
        pose = new Pose(pos + rot * offset, rot);
        return true;
    }

    private static Transform CurrentTarget(in PatternContext ctx)
        => ctx.targets != null ? ctx.targets.CurrentTarget : null;
}

// "어디에" — 기준점 하나를 해석(ctx)하고, 그 둘레에 모양을 그려 점 목록을 낸다.
// Collect는 대기 · 부수효과가 없다. 점을 넣는 순서 = 실행 순서(간격은 PointEmitter가).
public abstract class Placement : MonoBehaviour
{
    [SerializeField] protected Anchor _anchor;

    public virtual void Collect(in PatternContext ctx, List<Pose> results)
    {
        if (_anchor.Resolve(ctx, out Pose origin)) Generate(origin, results);
    }

    // 순수 기하 — origin 기준 상대 좌표만 계산한다
    protected abstract void Generate(Pose origin, List<Pose> results);

    // 모양이 정한 바깥 방향(dir)과 옵션으로 점 하나의 방향을 정한다
    protected static Quaternion Face(Quaternion inherit, Vector3 dir, PointFacing facing)
    {
        dir.y = 0f;
        if (facing == PointFacing.Inherit || dir.sqrMagnitude < 0.0001f) return inherit;
        return Quaternion.LookRotation(facing == PointFacing.Outward ? dir : -dir);
    }

#if UNITY_EDITOR
    // 모양 미리보기 — 기준점 대신 이 오브젝트 위치 · 방향을 원점으로 그린다
    protected virtual void OnDrawGizmosSelected()
    {
        var points = new List<Pose>();
        Generate(new Pose(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f)), points);

        Gizmos.color = new Color(1f, 0.45f, 0.1f);
        foreach (Pose p in points)
        {
            Gizmos.DrawWireSphere(p.position, 0.3f);
            Gizmos.DrawLine(p.position, p.position + p.forward * 0.6f);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
#endif
}
