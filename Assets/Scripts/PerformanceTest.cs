using UnityEngine;
using UnityEngine.Profiling;
using System.Diagnostics;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

public class ExcelPerformanceTest : MonoBehaviour
{
    private void Start()
    {
        RunTestAsync().Forget();
    }

    private async UniTaskVoid RunTestAsync()
    {
        UnityEngine.Debug.Log("<color=#00FFFF><b>=================================================</b></color>\n" +
                              "<color=#00FFFF><size=14><b> [Data-Driven] 대규모 엑셀 멀티-시트 파싱 프로파일링</b></size></color>\n" +
                              "<color=#00FFFF><b>=================================================</b></color>");

        // 1. 런타임에 모든 엑셀 강제 파싱 (동기)
        (long npoiTime, int npoiCount) = TestRuntimeNPOIParsing();

        // 2. 가비지 컬렉터 강제 호출 (명확한 비교를 위함)
        System.GC.Collect();
        await UniTask.DelayFrame(3);

        // 3. 미리 베이킹된 SO 파일 로드 (비동기 Addressables)
        (long soTime, int soCount) = await TestSOLoadingAsync();

        // 4. 최종 결과 요약 로그
        float improvement = soTime == 0 ? npoiTime : (float)npoiTime / soTime;

        string resultLog = $"<color=#FFFF00><size=15><b>[성능 비교 요약]</b></size></color>\n\n" +
                           $"<b>데이터 총 로드 건수 (모든 시트 통합)</b>: <color=#FFFFFF><b>{npoiCount:#,##0} 건</b></color>\n\n" +
                           $"<b> [최적화 전] NPOI 런타임 파싱 소요 시간</b>: <color=#FF4444><size=13><b>{npoiTime:#,##0} ms</b></size></color>\n" +
                           $"<b> [최적화 후] Addressables SO 비동기 로딩 시간</b>: <color=#00FF00><size=13><b>{soTime:#,##0} ms</b></size></color>\n\n" +
                           $" <b><color=#FFFF00>결론: 로딩 속도 약 {improvement:F1}배 향상</color></b>\n" +
                           $"<color=#00FFFF><b>=================================================</b></color>";

        UnityEngine.Debug.Log(resultLog);
    }

    // 튜플(소요시간, 파싱한 총 로우 개수) 반환
    private (long time, int count) TestRuntimeNPOIParsing()
    {
        Stopwatch sw = new Stopwatch();

        Profiler.BeginSample("Test_Runtime_NPOI_Parsing_ALL");
        sw.Start();

        string basePath = Application.dataPath + "/ExcelData/";
        int totalCount = 0;

        int monsterDBcnt = ParseMonsterExcel(basePath + "MonsterTable.xlsx");
        int itemDBcnt = ParseItemExcel(basePath + "ItemTable.xlsx");
        int dungeonDBcnt = ParseDungeonExcel(basePath + "DungeonTable.xlsx");
        int dropDBcnt = ParseDropTableExcel(basePath + "DropTable.xlsx");
        int charExpDBcnt = ParseCharacterExcel(basePath + "CharacterTable.xlsx");

        UnityEngine.Debug.Log($"<color=#FFFFFF><b>[NPOI 멀티-시트 로딩 세부 건수]</b></color>\n" +
                              $"Monster: {monsterDBcnt}건, Item: {itemDBcnt}건, Dungeon: {dungeonDBcnt}건, DropTable: {dropDBcnt}건, Character: {charExpDBcnt}건");

        sw.Stop();
        Profiler.EndSample();

        totalCount = monsterDBcnt + itemDBcnt + dungeonDBcnt + dropDBcnt + charExpDBcnt;
        return (sw.ElapsedMilliseconds, totalCount);
    }

    // 튜플(소요시간, 로드한 개수) 반환 - (Addressables 비동기 로딩 적용)
    private async UniTask<(long time, int count)> TestSOLoadingAsync()
    {
        Stopwatch sw = new Stopwatch();

        Profiler.BeginSample("Test_Runtime_SO_Loading_ALL");
        sw.Start();

        // DataManager와 동일한 Addressables 로드 방식
        var monsterTask = Addressables.LoadAssetAsync<MonsterDatabaseSO>("MonsterDatabase").ToUniTask();
        var itemTask = Addressables.LoadAssetAsync<ItemDatabaseSO>("ItemDatabase").ToUniTask();
        var dungeonTask = Addressables.LoadAssetAsync<DungeonDatabaseSO>("DungeonDatabase").ToUniTask();
        var dropTask = Addressables.LoadAssetAsync<DropTableDatabaseSO>("DropTableDatabase").ToUniTask();
        var charExpTask = Addressables.LoadAssetAsync<CharacterExpTableSO>("CharacterExpTable").ToUniTask();

        await UniTask.WhenAll(monsterTask, itemTask, dungeonTask, dropTask, charExpTask);

        var monsterDB = monsterTask.GetAwaiter().GetResult();
        var itemDB = itemTask.GetAwaiter().GetResult();
        var dungeonDB = dungeonTask.GetAwaiter().GetResult();
        var dropDB = dropTask.GetAwaiter().GetResult();
        var charExpDB = charExpTask.GetAwaiter().GetResult();

        int totalCount = 0;
        int monsterDBcnt = 0, itemDBcnt = 0, dungeonDBcnt = 0, dropDBcnt = 0, charExpDBcnt = 0;

        // Monster SO
        if (monsterDB != null)
        {
            if (monsterDB.MonsterBaseDatas != null) monsterDBcnt += monsterDB.MonsterBaseDatas.Count;
            if (monsterDB.MonsterLevelByStats != null) monsterDBcnt += monsterDB.MonsterLevelByStats.Count;
            if (monsterDB.MonsterConfigs != null) monsterDBcnt += monsterDB.MonsterConfigs.Count;
        }

        // Item SO
        if (itemDB != null)
        {
            if (itemDB.Equipments != null) itemDBcnt += itemDB.Equipments.Count;
            if (itemDB.Consumables != null) itemDBcnt += itemDB.Consumables.Count;
            if (itemDB.Materials != null) itemDBcnt += itemDB.Materials.Count;
            if (itemDB.StatPools != null) itemDBcnt += itemDB.StatPools.Count;
            if (itemDB.DecompositionData != null) itemDBcnt += itemDB.DecompositionData.Count;
            if (itemDB.UpgradeBookExpData != null) itemDBcnt += itemDB.UpgradeBookExpData.Count;
            if (itemDB.EquipmentLevelExpData != null) itemDBcnt += itemDB.EquipmentLevelExpData.Count;
            if (itemDB.EquipmentUpgradeCost != null) itemDBcnt += itemDB.EquipmentUpgradeCost.Count;
            if (itemDB.GradeConfigData != null) itemDBcnt += itemDB.GradeConfigData.Count;
        }

        // Dungeon SO
        if (dungeonDB != null)
        {
            if (dungeonDB.DungeonGroups != null) dungeonDBcnt += dungeonDB.DungeonGroups.Count;
        }

        // DropTable SO (수정됨: DungeonTables 제거, DropEntry의 데이터 개수를 계산하기 위해 하위 리스트 합산)
        if (dropDB != null)
        {
            if (dropDB.DropTables != null)
            {
                dropDBcnt += dropDB.DropTables.Count; // DropTable (그룹) 자체 개수
                foreach (var dropTable in dropDB.DropTables)
                {
                    if (dropTable.Entries != null)
                        dropDBcnt += dropTable.Entries.Count; // DropEntry (상세 아이템) 개수 추가
                }
            }
        }

        // Character SO
        if (charExpDB != null && charExpDB.ExpList != null) charExpDBcnt += charExpDB.ExpList.Count;

        UnityEngine.Debug.Log($"<color=#FFFFFF><b>[SO 베이킹 데이터 세부 건수]</b></color>\n" +
                              $"Monster: {monsterDBcnt}건, Item: {itemDBcnt}건, Dungeon: {dungeonDBcnt}건, DropTable: {dropDBcnt}건, Character: {charExpDBcnt}건");

        sw.Stop();
        Profiler.EndSample();

        totalCount = monsterDBcnt + itemDBcnt + dungeonDBcnt + dropDBcnt + charExpDBcnt;
        return (sw.ElapsedMilliseconds, totalCount);
    }

    // =========================================================================================
    // NPOI 파싱 헬퍼 함수들 (모든 시트 순회 적용)
    // =========================================================================================

    private int ParseMonsterExcel(string excelPath)
    {
        return CountSheets(excelPath, "MonsterData", "MonsterLevelByStat", "MapMonsterConfig");
    }

    private int ParseItemExcel(string excelPath)
    {
        return CountSheets(excelPath, "Equipment", "StatPool", "Consumable", "Material", "EquipmentDecomposition", "EquipmentUpgradeBookExp", "EquipmentLevelExpData", "EquipmentUpgradeCost", "GradeConfig");
    }

    private int ParseDungeonExcel(string excelPath)
    {
        return CountSheets(excelPath, "DungeonGroup", "DungeonTable");
    }

    private int ParseDropTableExcel(string excelPath)
    {
        // DropTable 엑셀 내의 시트 이름들 (이전 코드 참고)
        return CountSheets(excelPath, "DropTable", "DropEntry", "DungeonTable");
    }

    private int ParseCharacterExcel(string excelPath)
    {
        return CountSheets(excelPath, "Character_Stat_Table", "Common_Level_Exp_Table", "Weapon_Growth_Table", "Weapon_Stat_Table", "Weapon_Enhance_Cost_Table");
    }

    private int CountSheets(string excelPath, params string[] sheetNames)
    {
        int count = 0;
        if (!File.Exists(excelPath)) return count;
        using (FileStream stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            IWorkbook book = new XSSFWorkbook(stream);
            foreach (var sheetName in sheetNames)
            {
                count += CountSheetRows(GetSheetIgnoreCase(book, sheetName));
            }
        }
        return count;
    }

    // =========================================================================================
    // 시트 로우(Row) 카운트 헬퍼 함수
    // =========================================================================================
    private int CountSheetRows(ISheet sheet)
    {
        int count = 0;
        if (sheet == null) return count;

        for (int i = 1; i <= sheet.LastRowNum; i++)
        {
            IRow row = sheet.GetRow(i);
            // 비어있는 로우 무시
            if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank) continue;
            count++;

            // 실제 파싱 시 발생하는 부하(오버헤드)를 강제로 모방하기 위한 연산 처리 (I/O String 매핑)
            string dummy = row.GetCell(0)?.ToString();
        }
        return count;
    }

    // =========================================================================================
    // 공통 헬퍼 함수들
    // =========================================================================================
    private ISheet GetSheetIgnoreCase(IWorkbook book, string sheetName)
    {
        if (book == null) return null;
        for (int i = 0; i < book.NumberOfSheets; i++)
        {
            if (book.GetSheetName(i).Trim().Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                return book.GetSheetAt(i);
        }
        return null;
    }
}