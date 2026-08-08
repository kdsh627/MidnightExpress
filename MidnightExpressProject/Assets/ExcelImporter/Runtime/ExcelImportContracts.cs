using System;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class ExcelSheetAttribute : Attribute
{
    public ExcelSheetAttribute(string name, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Excel sheet name cannot be empty.", nameof(name));
        }

        Name = name;
        Required = required;
    }

    public string Name { get; }
    public bool Required { get; }
    public int HeaderRow { get; set; } = -1;
    public int DataStartRow { get; set; } = -1;
    public int DataStartColumn { get; set; } = -1;
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class ExcelPreserveWhitespaceAttribute : Attribute
{
}

public interface IExcelImportValidator
{
    /// <summary>
    /// Validates fully parsed workbook data before it replaces the current asset.
    /// Throw an exception containing actionable validation errors when the data is invalid.
    /// </summary>
    void ValidateImportedData();
}
