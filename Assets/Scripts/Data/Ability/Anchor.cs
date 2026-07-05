using UnityEngine;

// 배치(어디에 놓나)를 캡슐화한 공용 타입.
// 위치가 필요한 Effect(SpawnVFX·ScatterPattern·RadialBurstPattern 등)가 이 필드 하나로 원점을 해결한다.
// "원점을 어떻게 구하나"의 정의가 여기 한 곳에만 존재 → Effect마다 anchor/offset 을 복제하지 않는다.
//   차이는 asset 값(_source·_localOffset·_forwardDistance …)으로만 표현.
[System.Serializable]
public class Anchor
{
    public enum Source
    {
        Muzzle,        // ctx.Object (시전 기준점 = 총구)
        CasterRoot,    // ctx.CasterGO (시전자 루트)
        TargetPoint,   // ctx.TargetPoint (착탄/조준 월드 좌표)
        Named,         // ctx.CasterGO 의 VfxAnchorSet 에서 _namedId 로 조회
        CasterForward, // 시전자 전방 _forwardDistance m
    }

    [SerializeField] private Source _source = Source.TargetPoint;
    [Tooltip("_source == Named 일 때 VfxAnchorSet 에서 조회할 부착점 id")]
    [SerializeField] private string _namedId;
    [Tooltip("_source == CasterForward 일 때 전방 거리(m)")]
    [SerializeField] private float _forwardDistance = 0f;
    [SerializeField] private Vector3 _localOffset;
    [SerializeField] private Vector3 _localEuler;

    // 원점을 해결해 월드 pos/rot 을 낸다. (_localOffset·_localEuler 는 이미 반영됨)
    // 반환 Transform: attach(따라다니기)가 필요한 Effect만 사용, 없으면 null.
    public Transform Resolve(AbilityContext ctx, out Vector3 pos, out Quaternion rot)
    {
        Transform t = null;
        Vector3 basePos;
        Quaternion baseRot;

        switch (_source)
        {
            case Source.CasterRoot:
                t = ctx.CasterGO.transform;
                basePos = t.position; baseRot = t.rotation;
                break;

            case Source.CasterForward:
                t = ctx.CasterGO.transform;
                basePos = t.position + t.forward * _forwardDistance;
                baseRot = t.rotation;
                break;

            case Source.Named:
                if (ctx.CasterGO != null && ctx.CasterGO.TryGetComponent(out VfxAnchorSet set))
                    t = set.Get(_namedId);
                if (t == null)
                {
                    Debug.LogWarning($"[Anchor] 부착점 '{_namedId}' 없음 → ctx.Object 로 폴백");
                    t = ctx.Object;
                }
                basePos = t.position; baseRot = t.rotation;
                break;

            case Source.TargetPoint:
                basePos = ctx.TargetPoint; baseRot = Quaternion.identity;
                break;

            default: // Muzzle
                t = ctx.Object;
                basePos = t.position; baseRot = t.rotation;
                break;
        }

        rot = baseRot * Quaternion.Euler(_localEuler);
        pos = basePos + rot * _localOffset;
        return t;
    }
}
