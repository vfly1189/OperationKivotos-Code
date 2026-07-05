using UnityEngine;

// 소환된 오브젝트를 캐스터에 등록하는 계약(사망 시 정리 등). 선택적 —
// 구현이 없으면 SummonMonsters 는 등록 없이 소환만 한다.
public interface ISummonRegistry
{
    void RegisterSummoned(GameObject monster);
}
