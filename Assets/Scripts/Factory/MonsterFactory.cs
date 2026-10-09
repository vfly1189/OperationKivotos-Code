using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

// 몬스터의 유일한 입구. 외부(스포너·씬·소환 어빌리티)는 몬스터를 항상 여기서 받는다.
//  돌려주는 것은 "완성되어 켜진" 몬스터 — 호출부는 초기화 순서를 몰라도 된다.
//  출구(회수)는 여기가 아니라 Resource.Destroy → Pool.Push → OnDespawn (IMonsterLifecycle 계약).
public static class MonsterFactory
{

    // [핵심] 어드레서블 로드는 무조건 비동기이므로 팩토리 함수도 UniTask를 반환해야 합니다.
    public static UniTask<GameObject> CreateMonsterByAddressableKeyAsync(
        string monsterAddressableKey,
        int mapId,
        Vector3 position,
        Quaternion rotation,
        CancellationToken token = default)
    {
        MonsterBaseData monsterBaseData = Managers.Data.GetData<string, MonsterBaseData, MonsterAddressableMarker>(monsterAddressableKey);
        return CreateAsync(monsterBaseData, monsterAddressableKey, mapId, position, rotation, token);
    }

    public static UniTask<GameObject> CreateMonsterByMonsterIDAsync(
        int monsterID,
        int mapId,
        Vector3 position,
        Quaternion rotation,
        CancellationToken token = default)
    {
        MonsterBaseData monsterBaseData = Managers.Data.GetData<int, MonsterBaseData>(monsterID);
        return CreateAsync(monsterBaseData, monsterBaseData?.AddressableKey, mapId, position, rotation, token);
    }

    private static async UniTask<GameObject> CreateAsync(
        MonsterBaseData monsterBaseData,
        string prefabKey,
        int mapId,
        Vector3 position,
        Quaternion rotation,
        CancellationToken token)
    {
        // 1. 데이터 (동기 딕셔너리 접근)
        MapMonsterConfig mapConfig = Managers.Data.GetDict<int, MapMonsterConfig>()[mapId];

        if (monsterBaseData == null || mapConfig == null)
        {
            GameLog.LogError($"[MonsterFactory] 데이터 로드 실패. Key: {prefabKey}, MapID: {mapId}");
            return null;
        }

        bool isBossElite =
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Elite) ||
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Boss);

        // 2. 레벨 및 스탯 계산
        int spawnLevel = isBossElite ? mapConfig.EliteMonsterLevel : mapConfig.NormalMonsterLevel;
        var levelStat = Managers.Data.GetDict<int, MonsterLevelByStat>()[spawnLevel];

        // 3. 로드 (씬 이탈/디스폰으로 취소되면 예외 대신 null로 흡수 — 호출부의 null 방어와 일관)
        GameObject prefab;
        try
        {
            prefab = await Managers.Resource.LoadAsync<GameObject>(prefabKey, token: token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (prefab == null || token.IsCancellationRequested)
        {
            if (prefab == null) GameLog.LogError($"[MonsterFactory] 몬스터 생성 실패. Key: {prefabKey}");
            return null;
        }

        // 4. 생성 — 꺼진 채로 받는다 (Awake는 끝난 상태). 위치도 꺼진 상태에서 잡힌다.
        //    위치는 호출 시점의 값 — 로드 대기 중 포인트가 파괴돼도 영향 없다.
        //    단계마다 계측 마커 (계획서 0-4 · Phase 1 스폰 1회 분해)
        GameObject monsterObj;
        using (FieldMetrics.MonsterPop())   // 안쪽 분해(씬 찾기 · SetParent · 위치)도 이 구간에서만 센다
            monsterObj = Managers.Resource.Instantiate(prefab, position, rotation, activate: false);

        // 5. 데이터 주입 → 생애 시작 → 켜기 (순서 고정: OnSpawn이 채운 HP/HP바가 SetStat 값을 읽는다)
        using (FieldMetrics.SpawnStat.Auto())
        {
            MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();
            if (monsterStat != null)
                monsterStat.SetStat(monsterBaseData, levelStat);
        }

        using (FieldMetrics.SpawnOnSpawn.Auto())
        {
            if (monsterObj.TryGetComponent(out IMonsterLifecycle lifecycle))
                lifecycle.OnSpawn(new SpawnContext(monsterBaseData, position, rotation));
        }

        using (FieldMetrics.SpawnActivate.Auto())
            monsterObj.SetActive(true);         // OnEnable — 애니메이터 Rebind · NavMeshAgent 켜기

        return monsterObj;
    }
}
