using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// 원샷 VFX(총구 화염, 피격 스파크, 시전 오라, 착탄 등)를 앵커 위치에 스폰한다.
// 배치(어디에 놓나)는 공용 Anchor 타입이 담당 → SpawnVFX 는 '스폰 + 재생 + (선택)attach' 만.
// 반환/파괴는 프리팹의 AutoReturnToPool 이 처리하므로 여기선 대기하지 않는다.
// 상태 없음 → CreateRuntime() => this (공유 안전, GC 0).
[CreateAssetMenu(menuName = "Kivotos/Effect/SpawnVFX")]
public class SpawnVFX : EffectData, IEffect
{
    [Header("Prefab")]
    [Tooltip("AutoReturnToPool 이 붙은 원샷 VFX 프리팹")]
    [SerializeField] private GameObject _vfxPrefab;

    [Header("Placement")]
    [SerializeField] private Anchor _placement = new();
    [Tooltip("앵커에 parent 로 붙여 따라다니게 한다(예: 연사 중 총구 추적)")]
    [SerializeField] private bool _attach = false;

    public override IEffect CreateRuntime() => this;

    public UniTask ExecuteAsync(AbilityContext ctx, CancellationToken token)
    {
        if (_vfxPrefab == null) return UniTask.CompletedTask;

        Transform anchor = _placement.Resolve(ctx, out Vector3 pos, out Quaternion rot);

        GameObject go = Managers.Resource.Instantiate(_vfxPrefab, pos, rot);
        if (go == null) return UniTask.CompletedTask;

        if (_attach && anchor != null)
            go.transform.SetParent(anchor, worldPositionStays: true); // 앵커 따라다님

        // 풀 재사용 대비: 위치를 잡은 '뒤' 명시적으로 재생.
        // - Play On Awake 는 풀 최초 생성 때만 돌아 재사용 시 재생이 안 될 수 있음
        // - Clear 로 지난 생애(다른 위치)의 잔여 파티클을 제거 → 엉뚱한 위치에 뿜는 것 방지
        ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>(true);
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }

        return UniTask.CompletedTask; // 대기하지 않음 — 다음 Effect 를 막지 않는다.
    }
}
