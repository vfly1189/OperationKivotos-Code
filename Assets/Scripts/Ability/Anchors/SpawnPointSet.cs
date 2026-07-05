using System.Collections.Generic;
using UnityEngine;

// 캐스터가 소환 스폰포인트들을 보관한다. (VfxAnchorSet 과 같은 컨셉 — 씬 위치 홀더)
// 지점은 인스펙터로 직접 넣거나, 씬 셋업이 SetPoints 로 주입한다(예: Map → Boss).
// SO(Effect)는 씬을 못 참조하므로, SummonMonsters 는 ctx.CasterGO 에서
// ISpawnPointProvider 로 이 컴포넌트를 조회한다.
public class SpawnPointSet : MonoBehaviour, ISpawnPointProvider
{
    [SerializeField] private List<Transform> _points = new();

    public void SetPoints(IEnumerable<Transform> points) => _points = new List<Transform>(points);
    public IReadOnlyList<Transform> GetSpawnPoints() => _points;
}
