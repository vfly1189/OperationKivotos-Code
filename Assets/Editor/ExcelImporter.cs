using UnityEngine;
using UnityEditor;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System;
using System.Linq; // Dictionary.Values.ToList() 등에 사용

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
            List<StatPoolData> statPoolList = new List<StatPoolData>(); // 추가됨

            // 1. Equipment 시트 파싱 (수정됨)
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
                    data.Name = GetCellString(row.GetCell(3));

                    // --- Equipment 전용 속성 ---
                    data.EquipPart = GetCellString(row.GetCell(4));
                    data.Tier = (int)(row.GetCell(5)?.NumericCellValue ?? 0);

                    // 수식이 적용된 셀의 결과값을 파싱
                    data.MainStatPoolID = GetNumericValue(row.GetCell(6));
                    data.SubStatPoolID = GetNumericValue(row.GetCell(7));

                    data.Description = GetCellString(row.GetCell(8)); // 엑셀 구조상 8번째(I열)로 Description이 당겨짐

                    equipList.Add(data);
                }
            }

            // 2. StatPool 시트 파싱 (추가됨)
            ISheet statSheet = book.GetSheet("StatPool");
            if (statSheet != null)
            {
                // PoolID를 Key로 묶어주기 위한 임시 딕셔너리
                Dictionary<int, StatPoolData> poolDict = new Dictionary<int, StatPoolData>();

                for (int i = 1; i <= statSheet.LastRowNum; i++)
                {
                    IRow row = statSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    int poolId = (int)row.GetCell(0).NumericCellValue;

                    // 딕셔너리에 없으면 새로 생성
                    if (!poolDict.ContainsKey(poolId))
                    {
                        poolDict[poolId] = new StatPoolData()
                        {
                            PoolID = poolId,
                            Note = GetCellString(row.GetCell(5)) // Note가 5번째(F열)에 있음
                        };
                    }

                    // Entry 생성 및 파싱
                    StatPoolEntry entry = new StatPoolEntry();
                    string statTypeStr = GetCellString(row.GetCell(1));

                    if (Enum.TryParse(statTypeStr, true, out EStatType parsedType))
                        entry.StatType = parsedType;
                    else
                        Debug.LogWarning($"[StatPool] 알 수 없는 StatType: {statTypeStr} (Row: {i})");

                    entry.Weight = (int)(row.GetCell(2)?.NumericCellValue ?? 0);
                    entry.BaseValue = (float)(row.GetCell(3)?.NumericCellValue ?? 0f);
                    entry.UpgradeValue = (float)(row.GetCell(4)?.NumericCellValue ?? 0f);

                    // 해당 Pool의 리스트에 추가
                    poolDict[poolId].Entries.Add(entry);
                }

                // 딕셔너리 Values를 List로 변환
                statPoolList = poolDict.Values.ToList();
            }

            // 3. Consumable 시트 파싱 (기존과 동일)
            ISheet consumeSheet = book.GetSheet("Consumable");
            if (consumeSheet != null)
            {
                // ... 기존 Consumable 파싱 로직 ...
                for (int i = 1; i <= consumeSheet.LastRowNum; i++)
                {
                    IRow row = consumeSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    ConsumableData data = new ConsumableData();
                    data.ID = (int)row.GetCell(0).NumericCellValue;
                    data.IconKey = GetCellString(row.GetCell(1));
                    data.Grade = ParseGrade(GetCellString(row.GetCell(2)));
                    data.Name = GetCellString(row.GetCell(3));
                    data.EffectValue = (float)(row.GetCell(4)?.NumericCellValue ?? 0f);
                    data.Duration = (float)(row.GetCell(5)?.NumericCellValue ?? 0f);
                    data.MaxStack = (int)(row.GetCell(6)?.NumericCellValue ?? 1);
                    data.Description = GetCellString(row.GetCell(7));

                    consumeList.Add(data);
                }
            }

            // 4. Material 시트 파싱 (기존과 동일)
            ISheet matSheet = book.GetSheet("Material");
            if (matSheet != null)
            {
                // ... 기존 Material 파싱 로직 ...
                for (int i = 1; i <= matSheet.LastRowNum; i++)
                {
                    IRow row = matSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank)
                        continue;

                    MaterialData data = new MaterialData();
                    data.ID = (int)row.GetCell(0).NumericCellValue;
                    data.IconKey = GetCellString(row.GetCell(1));
                    data.Grade = ParseGrade(GetCellString(row.GetCell(2)));
                    data.Name = GetCellString(row.GetCell(3));
                    data.Tier = (int)(row.GetCell(4)?.NumericCellValue ?? 0);
                    data.MaxStack = (int)(row.GetCell(5)?.NumericCellValue ?? 9999);
                    data.Description = GetCellString(row.GetCell(6));

                    matList.Add(data);
                }
            }

            SaveToScriptableObject(equipList, consumeList, matList, statPoolList); // 파라미터 추가
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

    // statPools 파라미터 추가
    private static void SaveToScriptableObject(List<EquipmentData> equips, List<ConsumableData> consumes, List<MaterialData> mats, List<StatPoolData> statPools)
    {
        string assetPath = "Assets/Resources_moved/Data/ItemDatabase.asset";
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
        database.StatPools = statPools; // 할당

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[멀티 시트 파싱 완료] 장비: {equips.Count}개, 스탯풀: {statPools.Count}개, 소모품: {consumes.Count}개, 재료: {mats.Count}개 변환 완료!");
    }
}
