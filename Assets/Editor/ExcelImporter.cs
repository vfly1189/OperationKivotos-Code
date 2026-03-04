using UnityEngine;
using UnityEditor;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;

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

            // 파싱된 데이터를 담을 리스트 준비
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
                    data.ID = (int)row.GetCell(0).NumericCellValue;
                    data.IconKey = row.GetCell(1).StringCellValue ?? "";
                    data.Name = row.GetCell(2)?.StringCellValue ?? "";
                    data.EquipPart = row.GetCell(3)?.StringCellValue ?? "";
                    data.Tier = (int)(row.GetCell(4)?.NumericCellValue ?? 0);
                    data.MaxHP = (float)(row.GetCell(5)?.NumericCellValue ?? 0f);
                    data.Attack = (float)(row.GetCell(6)?.NumericCellValue ?? 0f);
                    data.Defense = (float)(row.GetCell(7)?.NumericCellValue ?? 0f);
                    data.MoveSpeed = (float)(row.GetCell(8)?.NumericCellValue ?? 0f);
                    data.Description = row.GetCell(9)?.StringCellValue ?? "";

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
                    data.ID = (int)row.GetCell(0).NumericCellValue;
                    data.IconKey = row.GetCell(1).StringCellValue ?? "";
                    data.Name = row.GetCell(2)?.StringCellValue ?? "";
                    data.EffectValue = (float)(row.GetCell(3)?.NumericCellValue ?? 0f);
                    data.Duration = (float)(row.GetCell(4)?.NumericCellValue ?? 0f);
                    data.MaxStack = (int)(row.GetCell(5)?.NumericCellValue ?? 1);
                    data.Description = row.GetCell(6)?.StringCellValue ?? "";

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
                    data.ID = (int)row.GetCell(0).NumericCellValue;
                    data.IconKey = row.GetCell(1).StringCellValue ?? "";
                    data.Name = row.GetCell(2)?.StringCellValue ?? "";
                    data.Tier = (int)(row.GetCell(3)?.NumericCellValue ?? 0);
                    data.MaxStack = (int)(row.GetCell(4)?.NumericCellValue ?? 9999);
                    data.Description = row.GetCell(5)?.StringCellValue ?? "";

                    matList.Add(data);
                }
            }

            // SO에 저장
            SaveToScriptableObject(equipList, consumeList, matList);
        }
    }

    private static void SaveToScriptableObject(List<EquipmentData> equips, List<ConsumableData> consumes, List<MaterialData> mats)
    {
        string assetPath = "Assets/Resources/Data/ItemDatabase.asset";
        if (!Directory.Exists(Application.dataPath + "/Resources/Data"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Resources/Data");
        }

        ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            AssetDatabase.CreateAsset(database, assetPath);
        }

        // 각각의 리스트에 데이터 덮어씌우기
        database.Equipments = equips;
        database.Consumables = consumes;
        database.Materials = mats;

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[멀티 시트 파싱 완료] 장비: {equips.Count}개, 소모품: {consumes.Count}개, 재료: {mats.Count}개 변환 완료!");
    }
}
