using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UnityEditor;
using UnityEngine;

public class ExcelImporter : AssetPostprocessor
{
    private sealed class ExcelAssetInfo
    {
        public Type AssetType { get; set; }
        public ExcelAssetAttribute Attribute { get; set; }

        public string ExcelName => string.IsNullOrWhiteSpace(Attribute.ExcelName)
            ? AssetType.Name
            : Attribute.ExcelName.Trim();
    }

    private sealed class ColumnBinding
    {
        public int ColumnIndex { get; set; }
        public string HeaderName { get; set; }
        public FieldInfo EntityField { get; set; }
        public bool PreserveWhitespace { get; set; }
    }

    private sealed class ParsedSheet
    {
        public FieldInfo AssetField { get; set; }
        public object Entities { get; set; }
        public string SheetName { get; set; }
        public int RowCount { get; set; }
    }

    private static List<ExcelAssetInfo> cachedInfos;
    private static bool isApplyingImport;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (isApplyingImport)
        {
            return;
        }

        bool importedAny = false;

        foreach (string path in importedAssets)
        {
            if (!IsExcelPath(path))
            {
                continue;
            }

            string excelName = Path.GetFileNameWithoutExtension(path);
            if (excelName.StartsWith("~$", StringComparison.Ordinal))
            {
                continue;
            }

            if (cachedInfos == null)
            {
                cachedInfos = FindExcelAssetInfos();
            }

            ExcelAssetInfo info = cachedInfos.Find(candidate =>
                string.Equals(candidate.ExcelName, excelName, StringComparison.OrdinalIgnoreCase));

            if (info == null)
            {
                continue;
            }

            try
            {
                ImportExcelAtomically(path, info);
                importedAny = true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Excel import failed for '{path}'. The existing ScriptableObject was not changed.\n{exception}");
            }
        }

        if (!importedAny)
        {
            return;
        }

        try
        {
            isApplyingImport = true;
            AssetDatabase.SaveAssets();
        }
        finally
        {
            isApplyingImport = false;
        }
    }

    private static bool IsExcelPath(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ExcelAssetInfo> FindExcelAssetInfos()
    {
        var result = new List<ExcelAssetInfo>();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                object[] attributes = type.GetCustomAttributes(typeof(ExcelAssetAttribute), false);
                if (attributes.Length == 0)
                {
                    continue;
                }

                if (!typeof(ScriptableObject).IsAssignableFrom(type))
                {
                    Debug.LogError(
                        $"Type '{type.FullName}' uses ExcelAssetAttribute but does not inherit ScriptableObject.");
                    continue;
                }

                result.Add(new ExcelAssetInfo
                {
                    AssetType = type,
                    Attribute = (ExcelAssetAttribute)attributes[0]
                });
            }
        }

        foreach (IGrouping<string, ExcelAssetInfo> duplicate in result.GroupBy(
                     info => info.ExcelName,
                     StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            Debug.LogError(
                $"Multiple ExcelAsset types target workbook '{duplicate.Key}': "
                + string.Join(", ", duplicate.Select(info => info.AssetType.FullName)));
        }

        return result;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private static void ImportExcelAtomically(string excelPath, ExcelAssetInfo info)
    {
        string assetPath = GetOutputAssetPath(excelPath, info);
        ScriptableObject existingAsset = AssetDatabase.LoadAssetAtPath(assetPath, info.AssetType) as ScriptableObject;
        ScriptableObject stagingAsset = ScriptableObject.CreateInstance(info.AssetType);

        if (stagingAsset == null)
        {
            throw new InvalidOperationException(
                $"Could not create a temporary instance of '{info.AssetType.FullName}'.");
        }

        try
        {
            if (existingAsset != null)
            {
                EditorUtility.CopySerialized(existingAsset, stagingAsset);
            }

            IWorkbook workbook = LoadBook(excelPath);
            List<ParsedSheet> parsedSheets = ParseWorkbook(workbook, info, stagingAsset);

            if (stagingAsset is IExcelImportValidator validator)
            {
                try
                {
                    validator.ValidateImportedData();
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        $"Workbook validation failed for '{excelPath}':\n{exception.Message}",
                        exception);
                }
            }

            if (existingAsset == null)
            {
                EnsureAssetFolder(Path.GetDirectoryName(assetPath));
                stagingAsset.hideFlags = HideFlags.NotEditable;
                AssetDatabase.CreateAsset(stagingAsset, assetPath);
                existingAsset = stagingAsset;
                stagingAsset = null;
            }
            else
            {
                EditorUtility.CopySerialized(stagingAsset, existingAsset);
                EditorUtility.SetDirty(existingAsset);
            }

            if (info.Attribute.LogOnImport)
            {
                string summary = string.Join(
                    ", ",
                    parsedSheets.Select(sheet => $"{sheet.SheetName}: {sheet.RowCount} rows"));
                Debug.Log($"Imported '{excelPath}' -> '{assetPath}' ({summary}).", existingAsset);
            }
        }
        finally
        {
            if (stagingAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(stagingAsset);
            }
        }
    }

    private static string GetOutputAssetPath(string excelPath, ExcelAssetInfo info)
    {
        string assetName = info.AssetType.Name + ".asset";
        string configuredPath = info.Attribute.AssetPath;
        string folderPath;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            folderPath = Path.GetDirectoryName(excelPath);
        }
        else
        {
            string normalized = NormalizeAssetPath(configuredPath.Trim());
            folderPath = normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : "Assets/" + normalized.TrimStart('/');
        }

        folderPath = NormalizeAssetPath(folderPath);
        if (string.IsNullOrEmpty(folderPath)
            || (!folderPath.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                && !folderPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"ExcelAsset AssetPath must resolve inside Assets. Actual path: '{folderPath}'.");
        }

        return folderPath.TrimEnd('/') + "/" + assetName;
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string normalized = NormalizeAssetPath(folderPath)?.TrimEnd('/');
        if (string.IsNullOrEmpty(normalized) || normalized == "Assets")
        {
            return;
        }

        if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Asset folder must be inside Assets: '{normalized}'.");
        }

        string[] segments = normalized.Split('/');
        string current = "Assets";

        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }

            current = next;
        }
    }

    private static IWorkbook LoadBook(string excelPath)
    {
        try
        {
            using (FileStream stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return string.Equals(Path.GetExtension(excelPath), ".xls", StringComparison.OrdinalIgnoreCase)
                    ? (IWorkbook)new HSSFWorkbook(stream)
                    : new XSSFWorkbook(stream);
            }
        }
        catch (Exception exception)
        {
            throw new InvalidDataException($"Could not open Excel workbook '{excelPath}'.", exception);
        }
    }

    private static List<ParsedSheet> ParseWorkbook(
        IWorkbook workbook,
        ExcelAssetInfo info,
        ScriptableObject stagingAsset)
    {
        var parsedSheets = new List<ParsedSheet>();
        FieldInfo[] assetFields = info.AssetType.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (FieldInfo assetField in assetFields)
        {
            ExcelSheetAttribute sheetAttribute = assetField.GetCustomAttribute<ExcelSheetAttribute>(true);
            bool isLegacyPublicField = assetField.IsPublic;

            if (!TryGetListElementType(assetField.FieldType, out Type entityType)
                || (sheetAttribute == null && !isLegacyPublicField))
            {
                continue;
            }

            string sheetName = sheetAttribute?.Name ?? assetField.Name;
            ISheet sheet = workbook.GetSheet(sheetName);

            if (sheet == null)
            {
                if (sheetAttribute != null && sheetAttribute.Required)
                {
                    throw new InvalidDataException(
                        $"Required sheet '{sheetName}' for field '{info.AssetType.Name}.{assetField.Name}' was not found.");
                }

                if (sheetAttribute != null)
                {
                    object emptyList = Activator.CreateInstance(typeof(List<>).MakeGenericType(entityType));
                    assetField.SetValue(stagingAsset, emptyList);
                    parsedSheets.Add(new ParsedSheet
                    {
                        AssetField = assetField,
                        Entities = emptyList,
                        SheetName = sheetName,
                        RowCount = 0
                    });
                }

                continue;
            }

            object entities = GetEntityListFromSheet(
                sheet,
                entityType,
                sheetAttribute != null && sheetAttribute.HeaderRow >= 0
                    ? sheetAttribute.HeaderRow
                    : info.Attribute.HeaderRow,
                sheetAttribute != null && sheetAttribute.DataStartRow >= 0
                    ? sheetAttribute.DataStartRow
                    : info.Attribute.DataStartRow,
                sheetAttribute != null && sheetAttribute.DataStartColumn >= 0
                    ? sheetAttribute.DataStartColumn
                    : info.Attribute.DataStartColumn,
                sheetAttribute != null,
                out int rowCount);

            assetField.SetValue(stagingAsset, entities);
            parsedSheets.Add(new ParsedSheet
            {
                AssetField = assetField,
                Entities = entities,
                SheetName = sheetName,
                RowCount = rowCount
            });
        }

        return parsedSheets;
    }

    private static bool TryGetListElementType(Type fieldType, out Type entityType)
    {
        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            entityType = fieldType.GetGenericArguments()[0];
            return true;
        }

        entityType = null;
        return false;
    }

    private static object GetEntityListFromSheet(
        ISheet sheet,
        Type entityType,
        int headerRowIndex,
        int dataStartRowIndex,
        int dataStartColumnIndex,
        bool requireAllSerializableFields,
        out int rowCount)
    {
        if (headerRowIndex < 0 || dataStartRowIndex < 0 || dataStartColumnIndex < 0)
        {
            throw new InvalidDataException(
                $"Sheet '{sheet.SheetName}' has a negative header/data index configuration.");
        }

        List<ColumnBinding> bindings = GetColumnBindings(
            sheet,
            entityType,
            headerRowIndex,
            dataStartColumnIndex,
            requireAllSerializableFields);

        Type listType = typeof(List<>).MakeGenericType(entityType);
        object list = Activator.CreateInstance(listType);
        MethodInfo listAddMethod = listType.GetMethod("Add", new[] { entityType });
        rowCount = 0;

        for (int rowIndex = dataStartRowIndex; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            IRow row = sheet.GetRow(rowIndex);
            if (row == null)
            {
                continue;
            }

            ICell entryCell = row.GetCell(dataStartColumnIndex);
            if (IsBlankCell(entryCell))
            {
                continue;
            }

            if (GetEffectiveCellType(entryCell) == CellType.String
                && entryCell.StringCellValue.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            object entity = CreateEntityFromRow(row, bindings, entityType, sheet.SheetName);
            listAddMethod.Invoke(list, new[] { entity });
            rowCount++;
        }

        return list;
    }

    private static List<ColumnBinding> GetColumnBindings(
        ISheet sheet,
        Type entityType,
        int headerRowIndex,
        int dataStartColumnIndex,
        bool requireAllSerializableFields)
    {
        IRow headerRow = sheet.GetRow(headerRowIndex);
        if (headerRow == null)
        {
            throw new InvalidDataException(
                $"Sheet '{sheet.SheetName}' does not contain header row {headerRowIndex + 1}.");
        }

        FieldInfo[] candidateFields = entityType
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => !field.IsStatic
                && !field.IsInitOnly
                && !field.IsNotSerialized
                && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null))
            .ToArray();

        var result = new List<ColumnBinding>();
        var boundFields = new HashSet<FieldInfo>();

        for (int columnIndex = dataStartColumnIndex; columnIndex < headerRow.LastCellNum; columnIndex++)
        {
            ICell cell = headerRow.GetCell(columnIndex);
            if (IsBlankCell(cell))
            {
                continue;
            }

            string headerName = GetCellAsString(cell, false).Trim();
            if (string.IsNullOrEmpty(headerName))
            {
                continue;
            }

            FieldInfo entityField = candidateFields.FirstOrDefault(field => field.Name == headerName)
                ?? candidateFields.FirstOrDefault(field =>
                    string.Equals(field.Name, headerName, StringComparison.OrdinalIgnoreCase));

            if (entityField == null)
            {
                continue;
            }

            if (!boundFields.Add(entityField))
            {
                throw new InvalidDataException(
                    $"Sheet '{sheet.SheetName}' header maps field '{entityType.Name}.{entityField.Name}' more than once.");
            }

            result.Add(new ColumnBinding
            {
                ColumnIndex = columnIndex,
                HeaderName = headerName,
                EntityField = entityField,
                PreserveWhitespace = entityField.GetCustomAttribute<ExcelPreserveWhitespaceAttribute>(true) != null
            });
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException(
                $"Sheet '{sheet.SheetName}' header row {headerRowIndex + 1} does not match any serializable fields on '{entityType.FullName}'.");
        }

        if (requireAllSerializableFields)
        {
            string[] missingFields = candidateFields
                .Where(field => !boundFields.Contains(field))
                .Select(field => field.Name)
                .ToArray();
            if (missingFields.Length > 0)
            {
                throw new InvalidDataException(
                    $"Sheet '{sheet.SheetName}' header row {headerRowIndex + 1} is missing required columns for "
                    + $"'{entityType.FullName}': {string.Join(", ", missingFields)}.");
            }
        }

        return result;
    }

    private static object CreateEntityFromRow(
        IRow row,
        IEnumerable<ColumnBinding> bindings,
        Type entityType,
        string sheetName)
    {
        object entity = Activator.CreateInstance(entityType);

        foreach (ColumnBinding binding in bindings)
        {
            ICell cell = row.GetCell(binding.ColumnIndex);
            if (IsBlankCell(cell))
            {
                continue;
            }

            try
            {
                object value = ConvertCellValue(
                    cell,
                    binding.EntityField.FieldType,
                    binding.PreserveWhitespace);
                binding.EntityField.SetValue(entity, value);
            }
            catch (Exception exception)
            {
                string rawValue = DescribeCellValue(cell);
                throw new InvalidDataException(
                    $"Invalid value in sheet '{sheetName}', row {row.RowNum + 1}, column {binding.ColumnIndex + 1} "
                    + $"('{binding.HeaderName}'). Value '{rawValue}' cannot be assigned to "
                    + $"{entityType.Name}.{binding.EntityField.Name} ({binding.EntityField.FieldType.Name}).",
                    exception);
            }
        }

        return entity;
    }

    private static object ConvertCellValue(ICell cell, Type declaredType, bool preserveWhitespace)
    {
        Type targetType = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        CellType cellType = GetEffectiveCellType(cell);

        if (targetType == typeof(string))
        {
            return GetCellAsString(cell, preserveWhitespace);
        }

        if (targetType.IsEnum)
        {
            return ConvertEnum(cell, cellType, targetType);
        }

        if (targetType == typeof(int))
        {
            return checked((int)ConvertIntegral(cell, cellType, int.MinValue, int.MaxValue));
        }

        if (targetType == typeof(long))
        {
            return ConvertIntegral(cell, cellType, long.MinValue, long.MaxValue);
        }

        if (targetType == typeof(short))
        {
            return checked((short)ConvertIntegral(cell, cellType, short.MinValue, short.MaxValue));
        }

        if (targetType == typeof(byte))
        {
            return checked((byte)ConvertIntegral(cell, cellType, byte.MinValue, byte.MaxValue));
        }

        if (targetType == typeof(float))
        {
            double value = ConvertFloatingPoint(cell, cellType);
            if (value < -float.MaxValue || value > float.MaxValue)
            {
                throw new OverflowException("Value is outside Single range.");
            }

            return (float)value;
        }

        if (targetType == typeof(double))
        {
            return ConvertFloatingPoint(cell, cellType);
        }

        if (targetType == typeof(decimal))
        {
            return Convert.ToDecimal(ConvertFloatingPoint(cell, cellType), CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool))
        {
            return ConvertBoolean(cell, cellType);
        }

        throw new NotSupportedException(
            $"Excel importer does not support field type '{declaredType.FullName}'.");
    }

    private static object ConvertEnum(ICell cell, CellType cellType, Type enumType)
    {
        if (cellType == CellType.Numeric)
        {
            long numericValue = ConvertIntegral(cell, cellType, long.MinValue, long.MaxValue);
            return Enum.ToObject(enumType, numericValue);
        }

        if (cellType != CellType.String)
        {
            throw new FormatException("Enum values must be names or integral numbers.");
        }

        string text = cell.StringCellValue.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new FormatException("Enum value cannot be empty.");
        }

        long combinedValue = 0;
        string[] names = text.Split(',');
        foreach (string rawName in names)
        {
            string name = rawName.Trim();
            string canonicalName = Enum.GetNames(enumType).FirstOrDefault(candidate =>
                string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
            if (canonicalName == null)
            {
                throw new FormatException($"'{name}' is not a named value of {enumType.Name}.");
            }

            object parsed = Enum.Parse(enumType, canonicalName, false);
            combinedValue |= Convert.ToInt64(parsed, CultureInfo.InvariantCulture);
        }

        return Enum.ToObject(enumType, combinedValue);
    }

    private static long ConvertIntegral(ICell cell, CellType cellType, long minimum, long maximum)
    {
        long value;

        if (cellType == CellType.Numeric)
        {
            double numeric = cell.NumericCellValue;
            if (double.IsNaN(numeric)
                || double.IsInfinity(numeric)
                || Math.Truncate(numeric) != numeric
                || numeric < minimum
                || numeric > maximum)
            {
                throw new FormatException("Expected an in-range integer without a fractional part.");
            }

            value = checked((long)numeric);
        }
        else if (cellType == CellType.String)
        {
            if (!long.TryParse(
                    cell.StringCellValue.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                throw new FormatException("Expected an invariant-culture integer.");
            }
        }
        else
        {
            throw new FormatException("Expected an integer cell or an integer string.");
        }

        if (value < minimum || value > maximum)
        {
            throw new OverflowException($"Integer must be between {minimum} and {maximum}.");
        }

        return value;
    }

    private static double ConvertFloatingPoint(ICell cell, CellType cellType)
    {
        double value;
        if (cellType == CellType.Numeric)
        {
            value = cell.NumericCellValue;
        }
        else if (cellType == CellType.String)
        {
            if (!double.TryParse(
                    cell.StringCellValue.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                throw new FormatException("Expected an invariant-culture number.");
            }
        }
        else
        {
            throw new FormatException("Expected a numeric cell or numeric string.");
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new FormatException("NaN and Infinity are not supported.");
        }

        return value;
    }

    private static bool ConvertBoolean(ICell cell, CellType cellType)
    {
        if (cellType == CellType.Boolean)
        {
            return cell.BooleanCellValue;
        }

        if (cellType == CellType.Numeric)
        {
            if (cell.NumericCellValue == 0d)
            {
                return false;
            }

            if (cell.NumericCellValue == 1d)
            {
                return true;
            }

            throw new FormatException("Boolean numeric values must be 0 or 1.");
        }

        if (cellType == CellType.String)
        {
            string text = cell.StringCellValue.Trim();
            if (bool.TryParse(text, out bool value))
            {
                return value;
            }

            if (text == "0")
            {
                return false;
            }

            if (text == "1")
            {
                return true;
            }
        }

        throw new FormatException("Boolean values must be true, false, 0, or 1.");
    }

    private static string GetCellAsString(ICell cell, bool preserveWhitespace)
    {
        string value;
        switch (GetEffectiveCellType(cell))
        {
            case CellType.String:
                value = cell.StringCellValue;
                break;
            case CellType.Numeric:
                double numeric = cell.NumericCellValue;
                value = Math.Truncate(numeric) == numeric
                    ? numeric.ToString("0", CultureInfo.InvariantCulture)
                    : numeric.ToString("G17", CultureInfo.InvariantCulture);
                break;
            case CellType.Boolean:
                value = cell.BooleanCellValue ? "true" : "false";
                break;
            case CellType.Blank:
                value = string.Empty;
                break;
            default:
                throw new FormatException("Cell cannot be represented as text.");
        }

        return preserveWhitespace ? value : value.Trim();
    }

    private static string DescribeCellValue(ICell cell)
    {
        try
        {
            return GetCellAsString(cell, true)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
        catch
        {
            return cell?.ToString() ?? "<null>";
        }
    }

    private static bool IsBlankCell(ICell cell)
    {
        if (cell == null)
        {
            return true;
        }

        CellType type = GetEffectiveCellType(cell);
        return type == CellType.Blank
            || (type == CellType.String && string.IsNullOrWhiteSpace(cell.StringCellValue));
    }

    private static CellType GetEffectiveCellType(ICell cell)
    {
        return cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
    }
}
