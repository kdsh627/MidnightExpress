using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UnityEditor;
using UnityEngine;

public class ExcelImporter : AssetPostprocessor
{
    class ExcelAssetInfo
    {
        public Type AssetType { get; set; }
        public ExcelAssetAttribute Attribute { get; set; }
        public string ExcelName
        {
            get
            {
                return string.IsNullOrEmpty(Attribute.ExcelName) ? AssetType.Name : Attribute.ExcelName;
            }
        }
    }

    static List<ExcelAssetInfo> cachedInfos = null; // Clear on compile.

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        bool imported = false;
        foreach (string path in importedAssets)
        {
            if (Path.GetExtension(path) == ".xls" || Path.GetExtension(path) == ".xlsx")
            {
                if (cachedInfos == null) cachedInfos = FindExcelAssetInfos();

                var excelName = Path.GetFileNameWithoutExtension(path);
                if (excelName.StartsWith("~$")) continue;

                ExcelAssetInfo info = cachedInfos.Find(i => i.ExcelName == excelName);

                if (info == null) continue;

                ImportExcel(path, info);
                imported = true;
            }
        }

        if (imported)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    static List<ExcelAssetInfo> FindExcelAssetInfos()
    {
        var list = new List<ExcelAssetInfo>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                var attributes = type.GetCustomAttributes(typeof(ExcelAssetAttribute), false);
                if (attributes.Length == 0) continue;
                var attribute = (ExcelAssetAttribute)attributes[0];
                var info = new ExcelAssetInfo()
                {
                    AssetType = type,
                    Attribute = attribute
                };
                list.Add(info);
            }
        }
        return list;
    }

    static UnityEngine.Object LoadOrCreateAsset(string assetPath, Type assetType)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

        var asset = AssetDatabase.LoadAssetAtPath(assetPath, assetType);

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance(assetType);
            AssetDatabase.CreateAsset((ScriptableObject)asset, assetPath);
            asset.hideFlags = HideFlags.NotEditable;
        }

        return asset;
    }

    static IWorkbook LoadBook(string excelPath)
    {
        using (FileStream stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (Path.GetExtension(excelPath) == ".xls") return new HSSFWorkbook(stream);
            else return new XSSFWorkbook(stream);
        }
    }

    static List<string> GetFieldNamesFromSheetHeader(ISheet sheet, int headerRowidx)
    {
        IRow headerRow = sheet.GetRow(headerRowidx); //수정

        var fieldNames = new List<string>();
        for (int i = 0; i < headerRow.LastCellNum; i++)
        {
            var cell = headerRow.GetCell(i);
            if (cell == null || cell.CellType == CellType.Blank) break;
            fieldNames.Add(cell.StringCellValue);
        }
        return fieldNames;
    }

    static object CellToFieldObject(ICell cell, FieldInfo fieldInfo, bool isFormulaEvalute = false)
    {
        var type = isFormulaEvalute ? cell.CachedFormulaResultType : cell.CellType;

        switch (type)
        {
            case CellType.String:
                string strValue = cell.StringCellValue.Trim();

                if (fieldInfo.FieldType.IsEnum)
                {
                    // 여러 개의 enum 이름이 콤마로 구분되어 있을 경우
                    if (strValue.Contains(","))
                    {
                        long combinedValue = 0;
                        var names = strValue.Split(',');
                        foreach (var name in names)
                        {
                            string n = name.Trim();
                            if (Enum.TryParse(fieldInfo.FieldType, n, true, out object value))
                                combinedValue |= Convert.ToInt64(value);
                            else
                                throw new Exception($"Invalid enum name '{n}' for {fieldInfo.FieldType.Name}");
                        }
                        return Enum.ToObject(fieldInfo.FieldType, combinedValue);
                    }

                    // 일반 Enum 이름 처리
                    return Enum.Parse(fieldInfo.FieldType, strValue, true);
                }

                // Enum이 아닐 경우 그냥 문자열로 반환
                return strValue;
            case CellType.Boolean:
                return cell.BooleanCellValue;
            case CellType.Numeric:
                return Convert.ChangeType(cell.NumericCellValue, fieldInfo.FieldType);
            case CellType.Formula:
                if (isFormulaEvalute) return null;
                return CellToFieldObject(cell, fieldInfo, true);
            default:
                if (fieldInfo.FieldType.IsValueType)
                {
                    return Activator.CreateInstance(fieldInfo.FieldType);
                }
                return null;
        }
    }

    static object CreateEntityFromRow(IRow row, List<string> columnNames, Type entityType, string sheetName)
    {
        var entity = Activator.CreateInstance(entityType);

        for (int i = 0; i < columnNames.Count; i++)
        {
            FieldInfo entityField = entityType.GetField(
                columnNames[i],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (entityField == null) continue;
            if (!entityField.IsPublic && entityField.GetCustomAttributes(typeof(SerializeField), false).Length == 0) continue;

            ICell cell = row.GetCell(i);
            if (cell == null) continue;

            try
            {
                object fieldValue = CellToFieldObject(cell, entityField);
                entityField.SetValue(entity, fieldValue);
            }
            catch
            {
                throw new Exception(string.Format("Invalid excel cell type at row {0}, column {1}, {2} sheet.", row.RowNum, cell.ColumnIndex, sheetName));
            }
        }
        return entity;
    }

    static object GetEntityListFromSheet(ISheet sheet, Type entityType, int headerRowIdx, int dataStartRowIdx, int dataStartColumnIdx)
    {
        List<string> excelColumnNames = GetFieldNamesFromSheetHeader(sheet, headerRowIdx);

        Type listType = typeof(List<>).MakeGenericType(entityType);
        MethodInfo listAddMethod = listType.GetMethod("Add", new Type[] { entityType });
        object list = Activator.CreateInstance(listType);

        // row of index 0 is header
        for (int i = dataStartRowIdx; i <= sheet.LastRowNum; i++)
        {
            IRow row = sheet.GetRow(i);
            if (row == null) break;

            ICell entryCell = row.GetCell(dataStartColumnIdx);
            if (entryCell == null || entryCell.CellType == CellType.Blank) break;

            // skip comment row
            if (entryCell.CellType == CellType.String && entryCell.StringCellValue.StartsWith("#")) continue;

            var entity = CreateEntityFromRow(row, excelColumnNames, entityType, sheet.SheetName);
            listAddMethod.Invoke(list, new object[] { entity });
        }
        return list;
    }

    static void ImportExcel(string excelPath, ExcelAssetInfo info)
    {
        string assetPath = "";
        string assetName = info.AssetType.Name + ".asset";

        if (string.IsNullOrEmpty(info.Attribute.AssetPath))
        {
            string basePath = Path.GetDirectoryName(excelPath);
            assetPath = Path.Combine(basePath, assetName);
        }
        else
        {
            var path = Path.Combine("Assets", info.Attribute.AssetPath);
            assetPath = Path.Combine(path, assetName);
        }
        UnityEngine.Object asset = LoadOrCreateAsset(assetPath, info.AssetType);

        IWorkbook book = LoadBook(excelPath);

        var assetFields = info.AssetType.GetFields();
        int sheetCount = 0;

        foreach (var assetField in assetFields)
        {
            ISheet sheet = book.GetSheet(assetField.Name);
            if (sheet == null) continue;

            Type fieldType = assetField.FieldType;
            if (!fieldType.IsGenericType || (fieldType.GetGenericTypeDefinition() != typeof(List<>))) continue;

            Type[] types = fieldType.GetGenericArguments();
            Type entityType = types[0];

            object entities = GetEntityListFromSheet(sheet, entityType, info.Attribute.HeaderRow, info.Attribute.DataStartRow, info.Attribute.DataStartColumn);
            assetField.SetValue(asset, entities);
            sheetCount++;
        }

        if (info.Attribute.LogOnImport)
        {
            Debug.Log(string.Format("Imported {0} sheets form {1}.", sheetCount, excelPath));
        }

        EditorUtility.SetDirty(asset);
    }
}
