using UnityEngine;
using UnityEditor;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System;
using System.Linq; // Dictionary.Values.ToList() 등에 사용

public class DungeonExcelImporter : EditorWindow
{
    [MenuItem("Data Tool/Import Dungeon Excel (Multi-Sheet)")]
    public static void ImportItemExcel()
    {
        string excelPath = Application.dataPath + "/ExcelData/DungeonTable.xlsx";
        if (!File.Exists(excelPath))
        {
            Debug.LogError("엑셀 파일을 찾을 수 없습니다: " + excelPath);
            return;
        }

        using (FileStream stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            IWorkbook book = new XSSFWorkbook(stream);

            List<DungeonGroup> dungeonGroups = new List<DungeonGroup>();
            
            // 1. DungeonGroup 시트 파싱
            ISheet dungeonGroupSheet = book.GetSheet("DungeonGroup");
            if (dungeonGroupSheet != null)
            {
                for (int i = 1; i <= dungeonGroupSheet.LastRowNum; i++)
                {
                    IRow row = dungeonGroupSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    DungeonGroup data = new DungeonGroup();
                    data.GroupID = GetNumericValue(row.GetCell(0));
                    data.DungeonName = GetCellString(row.GetCell(1));
                    data.Type = ParseDungeonType(GetCellString(row.GetCell(2)));

                    dungeonGroups.Add(data);
                }
            }

            // 2. DungeonTable 시트 파싱
            ISheet dungeonTableSheet = book.GetSheet("DungeonTable");
            if (dungeonTableSheet != null)
            {
                for (int i = 1; i <= dungeonTableSheet.LastRowNum; i++)
                {
                    IRow row = dungeonTableSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    DungeonData data = new DungeonData();
                    data.GroupID = GetNumericValue(row.GetCell(0));
                    data.DungeonID = GetNumericValue(row.GetCell(1));
                    data.Difficulty = ParseDungeonDifficulty(GetCellString(row.GetCell(2)));
                    data.RequiredLevel = GetNumericValue(row.GetCell(3));
                    data.ClearExp = GetNumericValue(row.GetCell(4));
                    data.ClearCredit = GetNumericValue(row.GetCell(5));
                    data.ClearDropTableID = GetNumericValue(row.GetCell(6));

                    foreach(var item in dungeonGroups)
                    {
                        if (item.GroupID == data.GroupID)
                        {
                            // 딕셔너리가 아닌 리스트에 추가해야 SO 파일에 저장됩니다.
                            item.DungeonDataList.Add(data);
                            break;
                        }
                    }
                }
            }



            SaveToScriptableObject(
                dungeonGroups
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

    private static DungeonType ParseDungeonType(string dungeonTypeStr)
    {
        if (string.IsNullOrEmpty(dungeonTypeStr)) return DungeonType.None;
        if(Enum.TryParse(dungeonTypeStr, true, out  DungeonType parsedDungeonType)) return parsedDungeonType;
        return DungeonType.None;
    }

    private static Define.DungeonDifficulty ParseDungeonDifficulty(string dungeonDifficultyStr)
    {
        if (string.IsNullOrEmpty(dungeonDifficultyStr)) return Define.DungeonDifficulty.Easy;
        if (Enum.TryParse(dungeonDifficultyStr, true, out Define.DungeonDifficulty parsedDungeonDifficulty)) return parsedDungeonDifficulty;
        return Define.DungeonDifficulty.Easy;
    }

    // statPools 파라미터 추가
    private static void SaveToScriptableObject(List<DungeonGroup> dungeonGroups)
    {
        string assetPath = "Assets/Resources_moved/Data/DungeonDatabase.asset";
        if (!Directory.Exists(Application.dataPath + "/Resources_moved/Data"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Resources_moved/Data");
        }

        DungeonDatabaseSO database = AssetDatabase.LoadAssetAtPath<DungeonDatabaseSO>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<DungeonDatabaseSO>();
            AssetDatabase.CreateAsset(database, assetPath);
        }

        database.DungeonGroups = dungeonGroups;

        
        
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        
    }
}
