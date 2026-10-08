using UnityEngine;

// 몬스터 생애 계약 (docs/MonsterSimulation/Audit/Lifecycle.md)
//  풀링 계약이 아니다 — 풀링 여부는 "생애를 여러 번 반복하느냐"일 뿐, 보스(비풀링)도 생애 1회로 같은 계약을 따른다.
//  입구 — MonsterFactory:   Instantiate(activate:false) → SetStat → OnSpawn(ctx) → SetActive(true)
//  출구 — Resource.Destroy: 풀링이면 Pool.Push에서, 아니면 Destroy 직전에 OnDespawn()
//  OnSpawn은 꺼진 상태에서, OnDespawn은 켜진 채로 불린다. Awake는 둘보다 항상 먼저 끝나 있다.
public interface IMonsterLifecycle
{
    void OnSpawn(SpawnContext ctx);
    void OnDespawn();
}

// 이번 생애의 스폰 정보 (읽기 전용). 매 스폰마다 새로 만들므로 struct — 할당 없음.
public readonly struct SpawnContext
{
    public readonly MonsterBaseData Data;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;

    public SpawnContext(MonsterBaseData data, Vector3 position, Quaternion rotation)
    {
        Data = data;
        Position = position;
        Rotation = rotation;
    }
}
