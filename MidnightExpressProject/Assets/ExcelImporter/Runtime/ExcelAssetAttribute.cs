using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ExcelAssetAttribute : Attribute
{
    public string AssetPath { get; set; }
    public string ExcelName { get; set; }
    public bool LogOnImport { get; set; }

    public int HeaderRow = 0; //변수 이름이 정리된 행
    public int DataStartRow = 1; //데이터가 시작되는 열
    public int DataStartColumn = 0; //데이터가 시작되는 행
}