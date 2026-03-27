using UnityEngine;
using UnityEditor;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Data; // Dictionary.Values.ToList() 등에 사용

public class DropTableExcelImporter : EditorWindow
{
    [MenuItem("Data Tool/Import DropTable Excel (Multi-Sheet)")]
    public static void ImportItemExcel()
    {
        string excelPath = Application.dataPath + "/ExcelData/DropTable.xlsx";
        if (!File.Exists(excelPath))
        {
            Debug.LogError("엑셀 파일을 찾을 수 없습니다: " + excelPath);
            return;
        }

        using (FileStream stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            IWorkbook book = new XSSFWorkbook(stream);

            List<DropTable> dropTables = new List<DropTable>();
            //List<DungeonTable> dungeonTables = new List<DungeonTable>();


            // 1. DropTable 시트 파싱 
            ISheet dropTableSheet = book.GetSheet("DropTable");
            if (dropTableSheet != null)
            {
                for (int i = 1; i <= dropTableSheet.LastRowNum; i++)
                {
                    IRow row = dropTableSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    DropTable data = new DropTable();
                    // --- BaseItemData 상속 속성 ---
                    data.DropTableID = GetNumericValue(row.GetCell(0));
                    data.DropTableName = GetCellString(row.GetCell(1));
                    data.Rolls = GetNumericValue(row.GetCell(2));

                    dropTables.Add(data);
                }
            }

            // 2. DropEntry 시트 파싱 
            ISheet dropEntrySheet = book.GetSheet("DropEntry");
            if (dropEntrySheet != null)
            {
                for (int i = 1; i <= dropEntrySheet.LastRowNum; i++)
                {
                    IRow row = dropEntrySheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    DropTableEntry data = new DropTableEntry();
                    // --- BaseItemData 상속 속성 ---

                    data.DropTableID = GetNumericValue(row.GetCell(0));
                    data.RewardType = ParseRewardType(GetCellString(row.GetCell(1)));
                    data.ItemID = GetNumericValue(row.GetCell(2));
                    data.Tier = GetNumericValue(row.GetCell(3));
                    data.Weight = GetNumericValue(row.GetCell(4));
                    data.MinCount = GetNumericValue(row.GetCell(5));
                    data.MaxCount = GetNumericValue(row.GetCell(6));

                    foreach(var item in dropTables)
                    {
                        if (item.DropTableID == data.DropTableID)
                            item.Entries.Add(data);
                    }
                }
            }

            //// 3. DungeonTable 시트 파싱 
            //ISheet dungeonTableSheet = book.GetSheet("DungeonTable");
            //if (dungeonTableSheet != null)
            //{
            //    for (int i = 1; i <= dungeonTableSheet.LastRowNum; i++)
            //    {
            //        IRow row = dungeonTableSheet.GetRow(i);
            //        if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
            //            continue;

            //        DungeonTable data = new DungeonTable();
            //        // --- BaseItemData 상속 속성 ---

            //        data.DungeonID = GetNumericValue(row.GetCell(0));
            //        data.DungeonName = GetCellString(row.GetCell(1));
            //        data.ClearExp = GetNumericValue(row.GetCell(2));
            //        data.ClearCredit = GetNumericValue(row.GetCell(3));
            //        data.ClearDropTableID = GetNumericValue(row.GetCell(4));
                 
            //        dungeonTables.Add(data);
            //    }
            //}



            SaveToScriptableObject(
                dropTables
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

    private static ItemGrade ParseGrade(string gradeStr)
    {
        if (string.IsNullOrEmpty(gradeStr)) return ItemGrade.Common;
        if (Enum.TryParse(gradeStr, true, out ItemGrade parsedGrade)) return parsedGrade;
        return ItemGrade.Common;
    }

    private static DropTableDefine.RewardType ParseRewardType(string rewardStr)
    {
        if (string.IsNullOrEmpty(rewardStr)) return DropTableDefine.RewardType.None;
        if (Enum.TryParse(rewardStr, true, out DropTableDefine.RewardType parsedRewardType)) return parsedRewardType;
        return DropTableDefine.RewardType.None;
    }

    // statPools 파라미터 추가
    private static void SaveToScriptableObject(
        List<DropTable> dropTables)
    {
        string assetPath = "Assets/Resources_moved/Data/DropTableDatabase.asset";
        if (!Directory.Exists(Application.dataPath + "/Resources_moved/Data"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Resources_moved/Data");
        }

        DropTableDatabaseSO database = AssetDatabase.LoadAssetAtPath<DropTableDatabaseSO>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<DropTableDatabaseSO>();
            AssetDatabase.CreateAsset(database, assetPath);
        }

        database.DropTables = dropTables;
        //database.DungeonTables = dungeonTables;

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

    }
}

