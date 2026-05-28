using Cysharp.Threading.Tasks;
using NPOI.SS.Formula.Functions;
using UnityEngine;
using UnityEngine.AddressableAssets;

public static class MonsterFactory
{

    public static GameObject CreateMonsterByAddressableKey(string monsterAddressableKey, int mapId, Transform spawnPoint)
    {
        // 1. 데이터 로드
        MonsterBaseData monsterBaseData = Managers.Data.GetData<string, MonsterBaseData, MonsterAddressableMarker>(monsterAddressableKey);
        MapMonsterConfig mapConfig = Managers.Data.GetDict<int, MapMonsterConfig>()[mapId];

        if (monsterBaseData == null || mapConfig == null) return null;

        // 2. 레벨 및 스탯 계산
        int spawnLevel = monsterBaseData.Grade == MonsterDefine.MonsterGrade.Elite
            ? mapConfig.EliteMonsterLevel : mapConfig.NormalMonsterLevel;
        var levelStat = Managers.Data.GetDict<int, MonsterLevelByStat>()[spawnLevel];

        // 3. 게임 오브젝트 비동기 생성 (어드레서블 로드)
        GameObject monsterObj = Managers.Resource.Instantiate(
            monsterBaseData.AddressableKey,
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (monsterObj == null) return null;

        // 4. 컴포넌트 초기화
        MonsterController monsterCtrl = monsterObj.GetComponent<MonsterController>();
        MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();

        if (monsterStat != null)
        {
            monsterStat.SetStat(monsterBaseData, levelStat);
        }

        return monsterObj;
    }

    // [핵심] 어드레서블 로드는 무조건 비동기이므로 팩토리 함수도 UniTask를 반환해야 합니다.
    public static async UniTask<GameObject> CreateMonsterByAddressableKeyAsync(
        string monsterAddressableKey,
        int mapId,
        Transform spawnPoint)
    {
        // 1. 데이터 로드 (동기 딕셔너리 접근)
        MonsterBaseData monsterBaseData = Managers.Data.GetData<string, MonsterBaseData, MonsterAddressableMarker>(monsterAddressableKey);
        MapMonsterConfig mapConfig = Managers.Data.GetDict<int, MapMonsterConfig>()[mapId];

        if (monsterBaseData == null || mapConfig == null)
        {
            Debug.LogError($"[MonsterFactory] 데이터 로드 실패. Key: {monsterAddressableKey}, MapID: {mapId}");
            return null;
        }

        bool isBossElite =
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Elite) ||
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Boss);

        // 2. 레벨 및 스탯 계산
        int spawnLevel = isBossElite ? mapConfig.EliteMonsterLevel : mapConfig.NormalMonsterLevel;
        var levelStat = Managers.Data.GetDict<int, MonsterLevelByStat>()[spawnLevel];

        // 3. 게임 오브젝트 비동기 생성 
        // 방법: 원본 프리팹 에셋을 비동기로 먼저 메모리에 로드(LoadAsync) 합니다.
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(monsterAddressableKey);

        if (prefab == null)
        {
            Debug.LogError($"[MonsterFactory] 프리팹 로드 실패. Key: {monsterAddressableKey}");
            return null;
        }

        // 4. 로드된 프리팹을 유니티 기본 Instantiate로 씬에 생성합니다. (동기 생성이라 안전함)
        GameObject monsterObj = Object.Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

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
        Transform spawnPoint)
    {
        // 1. 데이터 로드 (동기 딕셔너리 접근)
        MonsterBaseData monsterBaseData = Managers.Data.GetData<int, MonsterBaseData>(monsterID);
        MapMonsterConfig mapConfig = Managers.Data.GetDict<int, MapMonsterConfig>()[mapId];

        if (monsterBaseData == null || mapConfig == null)
        {
            Debug.LogError($"[MonsterFactory] 데이터 로드 실패. Key: {monsterID}, MapID: {mapId}");
            return null;
        }

        bool isBossElite =
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Elite) ||
            (monsterBaseData.Grade == MonsterDefine.MonsterGrade.Boss);

        // 2. 레벨 및 스탯 계산
        int spawnLevel = isBossElite ? mapConfig.EliteMonsterLevel : mapConfig.NormalMonsterLevel;
        var levelStat = Managers.Data.GetDict<int, MonsterLevelByStat>()[spawnLevel];

        // 3. 게임 오브젝트 비동기 생성 
        // 방법: 원본 프리팹 에셋을 비동기로 먼저 메모리에 로드(LoadAsync) 합니다.
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(monsterBaseData.AddressableKey);

        if (prefab == null)
        {
            Debug.LogError($"[MonsterFactory] 프리팹 로드 실패. Key: {monsterBaseData.AddressableKey}");
            return null;
        }

        // 4. 로드된 프리팹을 유니티 기본 Instantiate로 씬에 생성합니다. (동기 생성이라 안전함)
        GameObject monsterObj = Object.Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // 5. 컴포넌트 초기화 및 스탯 주입
        MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();
        if (monsterStat != null)
        {
            monsterStat.SetStat(monsterBaseData, levelStat);
        }

        return monsterObj;
    }
}
