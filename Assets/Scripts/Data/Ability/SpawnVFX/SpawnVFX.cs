using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 원샷 VFX(총구 화염, 피격 스파크, 시전 오라, 착탄 등)를 앵커 위치에 스폰한다.
// 로직은 전부 동일("풀에서 꺼내 어딘가에 놓는다") → 차이는 '어디에 놓느냐'뿐이라
// 앵커/오프셋만 asset 값으로 바꿔 재사용한다. (README 3-1: 로직 같음 + 값만 다름 → 클래스 1 + asset 다수)
//
// 반환/파괴는 프리팹에 붙인 AutoReturnToPool 이 처리하므로 여기선 대기하지 않는다.
// 상태 없음 → CreateRuntime() => this (공유 안전, GC 0).
public enum VFXAnchor
{
    Muzzle,      // ctx.Object  (시전 기준점 = 총구)
    CasterRoot,  // ctx.CasterGO (시전자 루트)
    TargetPoint, // ctx.TargetPoint (착탄/조준 월드 좌표)
    Named,       // ctx.CasterGO 의 VfxAnchorSet 에서 _anchorId 로 조회
}

[CreateAssetMenu(menuName = "Kivotos/Effect/SpawnVFX")]
public class SpawnVFX : EffectData, IEffect
{
    [Header("Prefab")]
    [Tooltip("AutoReturnToPool 이 붙은 원샷 VFX 프리팹")]
    [SerializeField] private GameObject _vfxPrefab;

    [Header("Placement")]
    [SerializeField] private VFXAnchor _anchor = VFXAnchor.Muzzle;
    [Tooltip("_anchor == Named 일 때, 시전자의 VfxAnchorSet 에서 조회할 부착점 id")]
    [SerializeField] private string _anchorId;
    [Tooltip("앵커에 parent 로 붙여 따라다니게 한다(예: 연사 중 총구 추적)")]
    [SerializeField] private bool _attach = false;
    [SerializeField] private Vector3 _localOffset;
    [SerializeField] private Vector3 _localEuler;

    public override IEffect CreateRuntime() => this;

    public UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        if (_vfxPrefab == null) return UniTask.CompletedTask;

        Transform anchor = ResolveAnchor(ctx, out Vector3 pos, out Quaternion rot);

        Quaternion finalRot = rot * Quaternion.Euler(_localEuler);
        GameObject go = Managers.Resource.Instantiate(_vfxPrefab, pos, finalRot);
        if (go == null) return UniTask.CompletedTask;

        Transform tr = go.transform;
        if (_attach && anchor != null)
        {
            tr.SetParent(anchor, worldPositionStays: true); // 앵커 따라다님
            tr.localPosition += _localOffset;               // 앵커 로컬 기준 오프셋
        }
        else
        {
            tr.position = pos + finalRot * _localOffset;     // 월드 기준 오프셋
        }

        // 풀 재사용 대비: 위치를 잡은 '뒤' 명시적으로 재생.
        // - Play On Awake 는 풀 최초 생성 때만 돌아 재사용 시 재생이 안 될 수 있음
        // - Clear 로 지난 생애(다른 위치)의 잔여 파티클을 제거 → 엉뚱한 위치에 뿜는 것 방지
        ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>(true);
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }

        // 대기하지 않음 — 총알 발사/다음 Effect 를 막지 않는다.
        return UniTask.CompletedTask;
    }

    private Transform ResolveAnchor(AbilityContext ctx, out Vector3 pos, out Quaternion rot)
    {
        switch (_anchor)
        {
            case VFXAnchor.CasterRoot:
                Transform t = ctx.CasterGO.transform;
                pos = t.position; rot = t.rotation;
                return t;

            case VFXAnchor.TargetPoint:
                pos = ctx.TargetPoint; rot = Quaternion.identity;
                return null;

            case VFXAnchor.Named:
                Transform named = null;
                if (ctx.CasterGO != null &&
                    ctx.CasterGO.TryGetComponent(out VfxAnchorSet set))
                    named = set.Get(_anchorId);

                if (named == null)
                {
                    Debug.LogWarning($"[SpawnVFX] 부착점 '{_anchorId}' 없음 → ctx.Object 로 폴백");
                    named = ctx.Object;
                }
                pos = named.position; rot = named.rotation;
                return named;

            default: // Muzzle
                pos = ctx.Object.position; rot = ctx.Object.rotation;
                return ctx.Object;
        }
    }
}
