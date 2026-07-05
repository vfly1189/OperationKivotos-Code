using System.Collections.Generic;
using UnityEngine;

// 소환/배치 지점을 제공하는 캐스터 쪽 계약.
// 스폰포인트는 보통 Map 소유 → 씬 셋업이 이 provider(캐스터 컴포넌트)에 주입한다.
// SO(Effect)는 씬을 참조 못 하므로, Effect는 ctx.CasterGO 에서 이 인터페이스로 조회한다.
public interface ISpawnPointProvider
{
    IReadOnlyList<Transform> GetSpawnPoints();
}
