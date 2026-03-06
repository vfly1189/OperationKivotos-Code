using UnityEngine;
using UnityEditor;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Globalization;

public class CharacterExcelImporter : EditorWindow
{
    [MenuItem("Data Tool/Import Character Excel (NPOI)")]
    public static void ImportCharacterExcel()
    {
        string excelPath = Application.dataPath + "/ExcelData/CharacterTable.xlsx";

        if (!File.Exists(excelPath))
        {
            Debug.LogError("[ExcelImporter] 엑셀 파일을 찾을 수 없습니다: " + excelPath);
            return;
        }

        string soFolderPath = "Assets/Resources_moved/Data/Characters";
        if (!Directory.Exists(soFolderPath)) Directory.CreateDirectory(soFolderPath);

        string weaponFolderPath = "Assets/Resources_moved/Data/Weapons";
        if (!Directory.Exists(weaponFolderPath)) Directory.CreateDirectory(weaponFolderPath);

        using (FileStream stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            IWorkbook book = new XSSFWorkbook(stream);

            // [핵심 추가] 엑셀 수식을 계산해주는 Evaluator 생성
            IFormulaEvaluator evaluator = book.GetCreationHelper().CreateFormulaEvaluator();

            // ==============================================================
            // 1. Character_Stat_Table 파싱
            // ==============================================================
            ISheet charSheet = GetSheetIgnoreCase(book, "Character_Stat_Table");
            if (charSheet != null)
            {
                int count = 0;
                for (int i = 1; i <= charSheet.LastRowNum; i++)
                {
                    IRow row = charSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank) continue;

                    int intID = (int)GetFloat(row, 0, evaluator);
                    string stringID = row.GetCell(1)?.StringCellValue ?? "";

                    if (string.IsNullOrEmpty(stringID)) continue;

                    string assetPath = $"{soFolderPath}/{stringID}.asset";
                    CharacterDataSO charSO = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(assetPath);
                    bool isNew = false;

                    if (charSO == null)
                    {
                        charSO = ScriptableObject.CreateInstance<CharacterDataSO>();
                        isNew = true;
                    }

                    charSO.id = intID;
                    charSO.key = stringID;
                    charSO.nameKR = row.GetCell(2)?.StringCellValue ?? "";

                    // 수식 지원 GetFloat 호출
                    charSO.baseHp = GetFloat(row, 3, evaluator);
                    charSO.baseAttack = GetFloat(row, 4, evaluator);
                    charSO.baseDefense = GetFloat(row, 5, evaluator);
                    charSO.hpGrowth = GetFloat(row, 6, evaluator);
                    charSO.attackGrowth = GetFloat(row, 7, evaluator);
                    charSO.defenseGrowth = GetFloat(row, 8, evaluator);
                    charSO.baseCritRate = GetFloat(row, 9, evaluator);
                    charSO.baseCritDamage = GetFloat(row, 10, evaluator);
                    charSO.baseEnergyRecharge = GetFloat(row, 11, evaluator);
                    charSO.baseMoveSpeed = GetFloat(row, 12, evaluator);
                    charSO.maxEnergy = GetFloat(row, 13, evaluator);

                    if (isNew) AssetDatabase.CreateAsset(charSO, assetPath);
                    else EditorUtility.SetDirty(charSO);

                    count++;
                }
                Debug.Log($"[Character Import] 캐릭터 SO 갱신 완료! (총 {count}명)");
            }

            // ==============================================================
            // 2. Common_Level_Exp_Table 파싱
            // ==============================================================
            ISheet expSheet = GetSheetIgnoreCase(book, "Common_Level_Exp_Table");
            if (expSheet != null)
            {
                string expAssetPath = "Assets/Resources_moved/Data/CharacterExpTable.asset";
                CharacterExpTableSO expSO = AssetDatabase.LoadAssetAtPath<CharacterExpTableSO>(expAssetPath);
                bool isExpNew = false;

                if (expSO == null)
                {
                    expSO = ScriptableObject.CreateInstance<CharacterExpTableSO>();
                    isExpNew = true;
                }

                expSO.ExpList.Clear();
                int expCount = 0;

                for (int i = 1; i <= expSheet.LastRowNum; i++)
                {
                    IRow row = expSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank) continue;

                    LevelExpData expData = new LevelExpData();
                    expData.Level = (int)GetFloat(row, 0, evaluator);
                    expData.RequireExp = (int)GetFloat(row, 1, evaluator);
                    expSO.ExpList.Add(expData);
                    expCount++;
                }

                if (isExpNew) AssetDatabase.CreateAsset(expSO, expAssetPath);
                else EditorUtility.SetDirty(expSO);

                Debug.Log($"[Exp Import] 경험치 테이블 SO 갱신 완료! (최대 {expCount} 레벨)");
            }

            // ==============================================================
            // 3. Weapon_Growth_Table 파싱 (메모리 임시 저장)
            // ==============================================================
            Dictionary<int, WeaponLevelStat> growthDict = new Dictionary<int, WeaponLevelStat>();
            int maxWeaponLevel = 25;

            ISheet weaponGrowthSheet = GetSheetIgnoreCase(book, "Weapon_Growth_Table");
            if (weaponGrowthSheet != null)
            {
                for (int i = 1; i <= weaponGrowthSheet.LastRowNum; i++)
                {
                    IRow row = weaponGrowthSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank) continue;

                    int level = (int)GetFloat(row, 0, evaluator);
                    WeaponLevelStat growthStat = new WeaponLevelStat();
                    growthStat.Level = level;
                    growthStat.Attack = GetFloat(row, 1, evaluator);
                    growthStat.HP = GetFloat(row, 2, evaluator);
                    growthStat.CritRate = GetFloat(row, 3, evaluator);
                    growthStat.CritDmg = GetFloat(row, 4, evaluator);

                    growthDict[level] = growthStat;
                    if (level > maxWeaponLevel) maxWeaponLevel = level;
                }
            }

            // ==============================================================
            // 4. Weapon_Stat_Table 파싱 (WeaponDataSO 생성)
            // ==============================================================
            ISheet weaponSheet = GetSheetIgnoreCase(book, "Weapon_Stat_Table");
            if (weaponSheet != null && growthDict.Count > 0)
            {
                int weaponCount = 0;
                for (int i = 1; i <= weaponSheet.LastRowNum; i++)
                {
                    IRow row = weaponSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank) continue;

                    int wpnID = (int)GetFloat(row, 0, evaluator);
                    string stringID = row.GetCell(1)?.StringCellValue ?? "";
                    if (string.IsNullOrEmpty(stringID)) continue;

                    string assetPath = $"{weaponFolderPath}/{stringID}.asset";
                    WeaponDataSO wpnSO = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(assetPath);
                    bool isNew = false;

                    if (wpnSO == null)
                    {
                        wpnSO = ScriptableObject.CreateInstance<WeaponDataSO>();
                        isNew = true;
                    }

                    wpnSO.id = wpnID;
                    wpnSO.key = stringID;
                    wpnSO.weaponName = row.GetCell(2)?.StringCellValue ?? "";
                    wpnSO.ownerCharID = (int)GetFloat(row, 3, evaluator);

                    float baseATK = GetFloat(row, 4, evaluator);
                    float baseHP = GetFloat(row, 5, evaluator);
                    float baseCritRate = GetFloat(row, 6, evaluator);
                    float baseCritDMG = GetFloat(row, 7, evaluator);

                    wpnSO.levelStats = new WeaponLevelStat[maxWeaponLevel];
                    for (int lv = 1; lv <= maxWeaponLevel; lv++)
                    {
                        WeaponLevelStat finalStat = new WeaponLevelStat();
                        finalStat.Level = lv;

                        if (growthDict.TryGetValue(lv, out WeaponLevelStat growth))
                        {
                            finalStat.Attack = baseATK + growth.Attack;
                            finalStat.HP = baseHP + growth.HP;
                            finalStat.CritRate = baseCritRate + growth.CritRate;
                            finalStat.CritDmg = baseCritDMG + growth.CritDmg;
                        }

                        wpnSO.levelStats[lv - 1] = finalStat;
                    }

                    if (isNew) AssetDatabase.CreateAsset(wpnSO, assetPath);
                    else EditorUtility.SetDirty(wpnSO);

                    weaponCount++;
                }
                Debug.Log($"[Weapon Import] 무기 데이터 SO 갱신 완료! (총 {weaponCount}개)");
            }

            // ==============================================================
            // 5. Weapon_Enhance_Cost_Table 파싱
            // ==============================================================
            ISheet costSheet = GetSheetIgnoreCase(book, "Weapon_Enhance_Cost_Table");
            if (costSheet != null)
            {
                string costAssetPath = "Assets/Resources_moved/Data/WeaponEnhanceCostTable.asset";
                WeaponEnhanceCostTableSO costSO = AssetDatabase.LoadAssetAtPath<WeaponEnhanceCostTableSO>(costAssetPath);
                bool isCostNew = false;

                if (costSO == null)
                {
                    costSO = ScriptableObject.CreateInstance<WeaponEnhanceCostTableSO>();
                    isCostNew = true;
                }

                costSO.CostList.Clear();
                int costCount = 0;

                for (int i = 1; i <= costSheet.LastRowNum; i++)
                {
                    IRow row = costSheet.GetRow(i);
                    if (row == null || row.GetCell(0) == null || row.GetCell(0).CellType == CellType.Blank) continue;

                    WeaponEnhanceCost costData = new WeaponEnhanceCost();
                    costData.TargetLevel = (int)GetFloat(row, 0, evaluator);

                    // 수식 계산 결과를 안전하게 int로 변환해서 가져옴
                    costData.RequireGold = (int)GetFloat(row, 1, evaluator);
                    costData.Material1ID = (int)GetFloat(row, 2, evaluator);
                    costData.Material1Count = (int)GetFloat(row, 3, evaluator);
                    costData.Material2ID = (int)GetFloat(row, 4, evaluator);
                    costData.Material2Count = (int)GetFloat(row, 5, evaluator);
                    costData.Material3ID = (int)GetFloat(row, 6, evaluator);
                    costData.Material3Count = (int)GetFloat(row, 7, evaluator);

                    costSO.CostList.Add(costData);
                    costCount++;
                }

                if (isCostNew) AssetDatabase.CreateAsset(costSO, costAssetPath);
                else EditorUtility.SetDirty(costSO);

                Debug.Log($"[Cost Import] 강화 비용 테이블 SO 갱신 완료! (총 {costCount}레벨치)");
            }
            else
            {
                Debug.LogError("[ExcelImporter] 'Weapon_Enhance_Cost_Table' 시트를 찾을 수 없습니다. (띄어쓰기 확인)");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    // ==============================================================
    // 헬퍼 함수들
    // ==============================================================

    // 시트 이름 앞뒤 공백을 무시하고 찾아주는 헬퍼
    private static ISheet GetSheetIgnoreCase(IWorkbook book, string sheetName)
    {
        for (int i = 0; i < book.NumberOfSheets; i++)
        {
            string name = book.GetSheetName(i);
            if (name.Trim().Equals(sheetName, StringComparison.OrdinalIgnoreCase))
            {
                return book.GetSheetAt(i);
            }
        }
        return null;
    }

    // 셀 값을 수식(Formula) 포함하여 안전하게 float으로 추출하는 헬퍼
    private static float GetFloat(IRow row, int cellIndex, IFormulaEvaluator evaluator)
    {
        ICell cell = row.GetCell(cellIndex, MissingCellPolicy.RETURN_BLANK_AS_NULL);
        if (cell == null) return 0f;

        try
        {
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    return (float)cell.NumericCellValue;

                case CellType.String:
                    {
                        var s = (cell.StringCellValue ?? "").Trim().Replace(",", "");
                        float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v);
                        return v;
                    }

                case CellType.Formula:
                    {
                        // 수식 계산
                        var v = evaluator.Evaluate(cell);
                        if (v == null) return 0f;

                        if (v.CellType == CellType.Numeric) return (float)v.NumberValue;

                        if (v.CellType == CellType.String)
                        {
                            var s = (v.StringValue ?? "").Trim().Replace(",", "");
                            float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var fv);
                            return fv;
                        }
                        return 0f;
                    }

                default:
                    return 0f;
            }
        }
        catch
        {
            // 오류 발생 시 크래시 방지
            return 0f;
        }
    }
}
