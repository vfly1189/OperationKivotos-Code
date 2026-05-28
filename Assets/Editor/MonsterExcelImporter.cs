using UnityEngine;
using UnityEditor;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System;
using System.Linq; // Dictionary.Values.ToList() 등에 사용

public class MonsterExcelImporter : EditorWindow
{
    [MenuItem("Data Tool/Import MonsterData Excel (Multi-Sheet)")]
    public static void ImportItemExcel()
    {
        string excelPath = Application.dataPath + "/ExcelData/MonsterTable.xlsx";
        if (!File.Exists(excelPath))
        {
            Debug.LogError("엑셀 파일을 찾을 수 없습니다: " + excelPath);
            return;
        }

        using (FileStream stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            IWorkbook book = new XSSFWorkbook(stream);

            List<MonsterBaseData> monsterBaseDatas = new List<MonsterBaseData>();
            List<MonsterLevelByStat> monsterLevelByStats = new List<MonsterLevelByStat>();
            List<MapMonsterConfig> mapMonsterConfigs = new List<MapMonsterConfig>();


            // 1. MonsterData 시트 파싱
            ISheet monsterDataSheet = book.GetSheet("MonsterData");
            if (monsterDataSheet != null)
            {
                for (int i = 1; i <= monsterDataSheet.LastRowNum; i++)
                {
                    IRow row = monsterDataSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    MonsterBaseData data = new MonsterBaseData();

                    data.ID = GetNumericValue(row.GetCell(0));
                    data.MonsterName = GetCellString(row.GetCell(1));
                    data.AddressableKey = GetCellString(row.GetCell(2));
                    data.SpawnType = ParseSpawnType(row.GetCell(3)?.StringCellValue);
                    data.Grade = ParseMonsterGrade(row.GetCell(4)?.StringCellValue);
                    data.BaseMaxHP = GetNumericValue(row.GetCell(5));
                    data.BaseAttack = GetNumericValue(row.GetCell(6));
                    data.BaseDefense = GetNumericValue(row.GetCell(7));
                    data.BaseSpeed = GetNumericValue(row.GetCell(8));
                    data.DropTableID = GetNumericValue(row.GetCell(9));
                    data.ExpReward = GetNumericValue(row.GetCell(10));
                    data.CreditReward = GetNumericValue(row.GetCell(11));


                    monsterBaseDatas.Add(data);
                }
            }

            // 2. MonsterLevelByStat 시트 파싱
            ISheet monsterLevelByStat = book.GetSheet("MonsterLevelByStat");
            if (monsterLevelByStat != null)
            {
                for (int i = 1; i <= monsterLevelByStat.LastRowNum; i++)
                {
                    IRow row = monsterLevelByStat.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    MonsterLevelByStat data = new MonsterLevelByStat();

                    data.Level = GetNumericValue(row.GetCell(0));
                    data.MaxHPRate = (float)(row.GetCell(1)?.NumericCellValue ?? 0f);
                    data.AttackRate = (float)(row.GetCell(2)?.NumericCellValue ?? 0f);
                    data.DefenseRate = (float)(row.GetCell(3)?.NumericCellValue ?? 0f);
                    data.CreditRate = (float)(row.GetCell(4)?.NumericCellValue ?? 0f);

                    monsterLevelByStats.Add(data);
                }
            }

            // 2. MonsterLevelByStat 시트 파싱
            ISheet mapMonsterConfig = book.GetSheet("MapMonsterConfig");
            if (mapMonsterConfig != null)
            {
                for (int i = 1; i <= mapMonsterConfig.LastRowNum; i++)
                {
                    IRow row = mapMonsterConfig.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    MapMonsterConfig data = new MapMonsterConfig();

                    data.MapID = GetNumericValue(row.GetCell(0));
                    data.MapName = GetCellString(row.GetCell(1));
                    data.NormalMonsterLevel = GetNumericValue(row.GetCell(2));
                    data.EliteMonsterLevel = GetNumericValue(row.GetCell(3));

                    mapMonsterConfigs.Add(data);
                }
            }


            SaveToScriptableObject(
                monsterBaseDatas, monsterLevelByStats, mapMonsterConfigs
                ); // 파라미터 추가
        }
    }

    // --- 헬퍼 함수들 ---

    // 수식(Formula)이 적용된 셀 값을 안전하게 가져오는 함수
    private static int GetNumericValue(ICell cell)
    {
        if (cell == null) return 0;
        if (cell.CellType == CellType.Formula)
        {
            // NPOI에서 수식 결과값을 읽으려면 NumericCellValue를 바로 읽으면 됩니다 (엑셀에서 저장시 값이 캐시되어 있어야 함)
            try { return (int)cell.NumericCellValue; }
            catch { return 0; }
        }
        else if (cell.CellType == CellType.Numeric)
        {
            return (int)cell.NumericCellValue;
        }
        return 0;
    }

    private static string GetCellString(ICell cell)
    {
        if (cell == null) return "";
        return cell.ToString();
    }

    private static MonsterDefine.MonsterSpawnType ParseSpawnType(string typeStr)
    {
        if (string.IsNullOrEmpty(typeStr)) return MonsterDefine.MonsterSpawnType.None;
        if (Enum.TryParse(typeStr, true, out MonsterDefine.MonsterSpawnType parsedType)) return parsedType;
        return MonsterDefine.MonsterSpawnType.None;
    }

    private static MonsterDefine.MonsterGrade ParseMonsterGrade(string gradeStr)
    {
        if (string.IsNullOrEmpty(gradeStr)) return MonsterDefine.MonsterGrade.None;
        if (Enum.TryParse(gradeStr, true, out MonsterDefine.MonsterGrade parsedGrade)) return parsedGrade;
        return MonsterDefine.MonsterGrade.None;
    }

    // statPools 파라미터 추가
    private static void SaveToScriptableObject(
        List<MonsterBaseData> monsterBaseDatas, List<MonsterLevelByStat> monsterLevelByStats, List<MapMonsterConfig> mapMonsterConfigs
        )
    {
        string assetPath = "Assets/Resources_moved/Data/MonsterDatabase.asset";
        if (!Directory.Exists(Application.dataPath + "/Resources_moved/Data"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Resources_moved/Data");
        }

        MonsterDatabaseSO database = AssetDatabase.LoadAssetAtPath<MonsterDatabaseSO>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<MonsterDatabaseSO>();
            AssetDatabase.CreateAsset(database, assetPath);
        }

        database.MonsterBaseDatas = monsterBaseDatas;
        database.MonsterLevelByStats = monsterLevelByStats;
        database.MonsterConfigs = mapMonsterConfigs;
        

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
