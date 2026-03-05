using UnityEngine;
using UnityEditor;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System;

public class ExcelImporter : EditorWindow
{
    [MenuItem("Data Tool/Import Item Excel (Multi-Sheet)")]
    public static void ImportItemExcel()
    {
        string excelPath = Application.dataPath + "/ExcelData/ItemTable.xlsx";
        if (!File.Exists(excelPath))
        {
            Debug.LogError("엑셀 파일을 찾을 수 없습니다: " + excelPath);
            return;
        }

        using (FileStream stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            IWorkbook book = new XSSFWorkbook(stream);

            List<EquipmentData> equipList = new List<EquipmentData>();
            List<ConsumableData> consumeList = new List<ConsumableData>();
            List<MaterialData> matList = new List<MaterialData>();

            // 1. Equipment 시트 파싱
            ISheet equipSheet = book.GetSheet("Equipment");
            if (equipSheet != null)
            {
                for (int i = 1; i <= equipSheet.LastRowNum; i++)
                {
                    IRow row = equipSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    EquipmentData data = new EquipmentData();
                    // --- BaseItemData 상속 속성 ---
                    data.ID = (int)row.GetCell(0).NumericCellValue;
                    data.IconKey = row.GetCell(1)?.StringCellValue ?? "";
                    data.Grade = ParseGrade(row.GetCell(2)?.StringCellValue);
                    data.Name = row.GetCell(3)?.StringCellValue ?? "";
                    data.Description = row.GetCell(10)?.StringCellValue ?? ""; // 설명이 10번에 있음

                    // --- Equipment 전용 속성 ---
                    data.EquipPart = row.GetCell(4)?.StringCellValue ?? "";
                    data.Tier = (int)(row.GetCell(5)?.NumericCellValue ?? 0);
                    data.MaxHP = (float)(row.GetCell(6)?.NumericCellValue ?? 0f);
                    data.Attack = (float)(row.GetCell(7)?.NumericCellValue ?? 0f);
                    data.Defense = (float)(row.GetCell(8)?.NumericCellValue ?? 0f);
                    data.MoveSpeed = (float)(row.GetCell(9)?.NumericCellValue ?? 0f);

                    equipList.Add(data);
                }
            }

            // 2. Consumable 시트 파싱
            ISheet consumeSheet = book.GetSheet("Consumable");
            if (consumeSheet != null)
            {
                for (int i = 1; i <= consumeSheet.LastRowNum; i++)
                {
                    IRow row = consumeSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    ConsumableData data = new ConsumableData();
                    // --- BaseItemData 상속 속성 ---
                    data.ID = (int)row.GetCell(0).NumericCellValue;
                    data.IconKey = row.GetCell(1)?.StringCellValue ?? "";
                    data.Grade = ParseGrade(row.GetCell(2)?.StringCellValue);
                    data.Name = row.GetCell(3)?.StringCellValue ?? "";
                    data.Description = row.GetCell(7)?.StringCellValue ?? ""; // 설명이 7번에 있음

                    // --- Consumable 전용 속성 ---
                    data.EffectValue = (float)(row.GetCell(4)?.NumericCellValue ?? 0f);
                    data.Duration = (float)(row.GetCell(5)?.NumericCellValue ?? 0f);
                    data.MaxStack = (int)(row.GetCell(6)?.NumericCellValue ?? 1);

                    consumeList.Add(data);
                }
            }

            // 3. Material 시트 파싱
            ISheet matSheet = book.GetSheet("Material");
            if (matSheet != null)
            {
                for (int i = 1; i <= matSheet.LastRowNum; i++)
                {
                    IRow row = matSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    MaterialData data = new MaterialData();
                    // --- BaseItemData 상속 속성 ---
                    data.ID = (int)row.GetCell(0).NumericCellValue;
                    data.IconKey = row.GetCell(1)?.StringCellValue ?? "";
                    data.Grade = ParseGrade(row.GetCell(2)?.StringCellValue);
                    data.Name = row.GetCell(3)?.StringCellValue ?? "";
                    data.Description = row.GetCell(6)?.StringCellValue ?? ""; // 설명이 6번에 있음

                    // --- Material 전용 속성 ---
                    data.Tier = (int)(row.GetCell(4)?.NumericCellValue ?? 0);
                    data.MaxStack = (int)(row.GetCell(5)?.NumericCellValue ?? 9999);

                    matList.Add(data);
                }
            }

            SaveToScriptableObject(equipList, consumeList, matList);
        }
    }

    // [추가] 엑셀의 문자열을 ItemGrade Enum으로 안전하게 변환하는 헬퍼 함수
    private static ItemGrade ParseGrade(string gradeStr)
    {
        if (string.IsNullOrEmpty(gradeStr))
            return ItemGrade.Common; // 기본값

        // 대소문자 무시하고 Enum 파싱 시도 (예: "common", "Epic" 모두 매핑)
        if (Enum.TryParse(gradeStr, true, out ItemGrade parsedGrade))
        {
            return parsedGrade;
        }

        Debug.LogWarning($"[ExcelImporter] 알 수 없는 등급입니다: {gradeStr}. Common으로 설정합니다.");
        return ItemGrade.Common;
    }

    private static void SaveToScriptableObject(List<EquipmentData> equips, List<ConsumableData> consumes, List<MaterialData> mats)
    {
        string assetPath = "Assets/Resources_moved/Data/ItemDatabase.asset"; // 경로 확인 필요
        if (!Directory.Exists(Application.dataPath + "/Resources_moved/Data"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Resources_moved/Data");
        }

        ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            AssetDatabase.CreateAsset(database, assetPath);
        }

        database.Equipments = equips;
        database.Consumables = consumes;
        database.Materials = mats;

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[멀티 시트 파싱 완료] 장비: {equips.Count}개, 소모품: {consumes.Count}개, 재료: {mats.Count}개 변환 완료!");
    }
}
