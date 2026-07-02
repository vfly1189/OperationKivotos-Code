using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public static class MonsterFactory
{

    // [핵심] 어드레서블 로드는 무조건 비동기이므로 팩토리 함수도 UniTask를 반환해야 합니다.
    public static async UniTask<GameObject> CreateMonsterByAddressableKeyAsync(
        string monsterAddressableKey,
        int mapId,
        Transform spawnPoint,
        CancellationToken token = default)
    {
        // 1. 데이터 로드 (동기 딕셔너리 접근)
        MonsterBaseData monsterBaseData = Managers.Data.GetData<string, MonsterBaseData, MonsterAddressableMarker>(monsterAddressableKey);
        MapMonsterConfig mapConfig = Managers.Data.GetDict<int, MapMonsterConfig>()[mapId];

        if (monsterBaseData == null || mapConfig == null)
        {
            GameLog.LogError($"[MonsterFactory] 데이터 로드 실패. Key: {monsterAddressableKey}, MapID: {mapId}");
            return null;
        }

        bool isBossElite =
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Elite) ||
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Boss);

        // 2. 레벨 및 스탯 계산
        int spawnLevel = isBossElite ? mapConfig.EliteMonsterLevel : mapConfig.NormalMonsterLevel;
        var levelStat = Managers.Data.GetDict<int, MonsterLevelByStat>()[spawnLevel];

        // 3. 로드-필요시-생성을 통합 API로 한 번에 (풀 경유 + 취소 토큰 전파)
        GameObject monsterObj = await Managers.Resource.InstantiateAsync(
            monsterAddressableKey, spawnPoint.position, spawnPoint.rotation, token: token);

        if (monsterObj == null)
        {
            GameLog.LogError($"[MonsterFactory] 몬스터 생성 실패. Key: {monsterAddressableKey}");
            return null;
        }

        // 5. 컴포넌트 초기화 및 스탯 주입
        MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();
        if (monsterStat != null)
        {
            monsterStat.SetStat(monsterBaseData, levelStat);
        }

        return monsterObj;
    }

    public static async UniTask<GameObject> CreateMonsterByMonsterIDAsync(
        int monsterID,
        int mapId,
        Transform spawnPoint,
        CancellationToken token = default)
    {
        // 1. 데이터 로드 (동기 딕셔너리 접근)
        MonsterBaseData monsterBaseData = Managers.Data.GetData<int, MonsterBaseData>(monsterID);
        MapMonsterConfig mapConfig = Managers.Data.GetDict<int, MapMonsterConfig>()[mapId];

        if (monsterBaseData == null || mapConfig == null)
        {
            GameLog.LogError($"[MonsterFactory] 데이터 로드 실패. Key: {monsterID}, MapID: {mapId}");
            return null;
        }

        bool isBossElite =
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Elite) ||
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Boss);

        // 2. 레벨 및 스탯 계산
        int spawnLevel = isBossElite ? mapConfig.EliteMonsterLevel : mapConfig.NormalMonsterLevel;
        var levelStat = Managers.Data.GetDict<int, MonsterLevelByStat>()[spawnLevel];

        // 3. 로드-필요시-생성을 통합 API로 한 번에 (풀 경유 + 취소 토큰 전파)
        GameObject monsterObj = await Managers.Resource.InstantiateAsync(
            monsterBaseData.AddressableKey, spawnPoint.position, spawnPoint.rotation, token: token);

        if (monsterObj == null)
        {
            GameLog.LogError($"[MonsterFactory] 몬스터 생성 실패. Key: {monsterBaseData.AddressableKey}");
            return null;
        }

        // 5. 컴포넌트 초기화 및 스탯 주입
        MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();
        if (monsterStat != null)
        {
            monsterStat.SetStat(monsterBaseData, levelStat);
        }

        return monsterObj;
    }
}
