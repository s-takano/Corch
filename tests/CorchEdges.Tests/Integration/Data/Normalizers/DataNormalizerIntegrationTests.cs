using System.Data;
using CorchEdges.Data.Abstractions;
using CorchEdges.Data.Mappers;
using CorchEdges.Data.Normalizers;
using CorchEdges.Data.Providers;

namespace CorchEdges.Tests.Integration.Data.Normalizers;

[Trait("Category", "Unit")]
public class DataNormalizerIntegrationTests
{
    private readonly ITableNormalizer _tableNormalizer;

    public DataNormalizerIntegrationTests()
    {
        _tableNormalizer = CreateEntityDataNormalizer();
    }

    private static ITableNormalizer CreateEntityDataNormalizer()
    {
        return new TableNormalizer(new ReflectionEntityMetadataProvider());
    }

    [Fact]
    public void NormalizeTableTypes_WithTypeConversion_ConvertsDataCorrectly()
    {
        // Arrange - Use the same Excel sheet name as defined in ContractCreationConfiguration
        var sourceTable = new DataTable("新規to業務管理");

        // Add columns with Japanese names matching ContractCreationConfiguration
        sourceTable.Columns.Add("契約ID", typeof(string));
        sourceTable.Columns.Add("物件No", typeof(string));
        sourceTable.Columns.Add("部屋No", typeof(string));
        sourceTable.Columns.Add("契約者1No", typeof(string)); // Note: "契約者1No" not "契約者No"
        sourceTable.Columns.Add("物件名", typeof(string));
        sourceTable.Columns.Add("契約者名", typeof(string));
        sourceTable.Columns.Add("進捗管理ステータス", typeof(string)); // Full name from config
        sourceTable.Columns.Add("契約ステータス", typeof(string)); // From config
        sourceTable.Columns.Add("入居申込日", typeof(string)); // From config
        sourceTable.Columns.Add("契約日", typeof(string));
        sourceTable.Columns.Add("入居予定日", typeof(string));
        sourceTable.Columns.Add("出力日時", typeof(string));
        sourceTable.Columns.Add("仲介手数料", typeof(string)); // Added BrokerageFee field

        // Add test data with string values that need type conversion
        var row = sourceTable.NewRow();
        row["契約ID"] = "CT-2024-001";
        row["物件No"] = "123"; // String -> int?
        row["部屋No"] = "101"; // String -> int?
        row["契約者1No"] = "456"; // String -> int? (correct column name)
        row["物件名"] = "サンライズマンション";
        row["契約者名"] = "田中太郎";
        row["進捗管理ステータス"] = "契約準備中";
        row["契約ステータス"] = "契約済";
        row["入居申込日"] = "2024-01-10"; // String -> DateOnly?
        row["契約日"] = "2024-01-15"; // String -> DateOnly?
        row["入居予定日"] = "2024-02-01"; // String -> DateOnly?
        row["出力日時"] = "2024-01-10T14:30:00"; // String -> DateTime?
        row["仲介手数料"] = "150000"; // String -> decimal?

        sourceTable.Rows.Add(row);

        // Act
        var result = _tableNormalizer.Normalize("corch_edges_raw.contract_creation", sourceTable);

        // Assert
        Assert.Equal("corch_edges_raw.contract_creation", result.TableName);
        Assert.Single(result.Rows);

        var resultRow = result.Rows[0];

        // Verify string values remain as strings
        Assert.Equal("CT-2024-001", resultRow["契約ID"]);
        Assert.Equal("サンライズマンション", resultRow["物件名"]);
        Assert.Equal("田中太郎", resultRow["契約者名"]);
        Assert.Equal("契約準備中", resultRow["進捗管理ステータス"]);
        Assert.Equal("契約済", resultRow["契約ステータス"]);

        // Verify type conversions from string to appropriate types
        Assert.Equal(123, resultRow["物件No"]); // string -> int?
        Assert.Equal(101, resultRow["部屋No"]); // string -> int?
        Assert.Equal(456, resultRow["契約者1No"]); // string -> int? (correct property name)
        Assert.Equal(DateOnly.Parse("2024-01-10"), resultRow["入居申込日"]); // string -> DateOnly?
        Assert.Equal(DateOnly.Parse("2024-01-15"), resultRow["契約日"]); // string -> DateOnly?
        Assert.Equal(DateOnly.Parse("2024-02-01"), resultRow["入居予定日"]); // string -> DateOnly?
        Assert.Equal(DateTime.Parse("2024-01-10T14:30:00"), resultRow["出力日時"]); // string -> DateTime?
        Assert.Equal(150000m, resultRow["仲介手数料"]); // string -> decimal?

        // Verify column types are properly converted to match ContractCreation entity
        Assert.Equal(typeof(string), result.Columns["契約ID"]!.DataType);
        Assert.Equal(typeof(int), result.Columns["物件No"]!.DataType);
        Assert.Equal(typeof(int), result.Columns["部屋No"]!.DataType);
        Assert.Equal(typeof(int), result.Columns["契約者1No"]!.DataType);
        Assert.Equal(typeof(string), result.Columns["物件名"]!.DataType);
        Assert.Equal(typeof(string), result.Columns["契約者名"]!.DataType);
        Assert.Equal(typeof(string), result.Columns["進捗管理ステータス"]!.DataType);
        Assert.Equal(typeof(string), result.Columns["契約ステータス"]!.DataType);
        Assert.Equal(typeof(DateOnly), result.Columns["入居申込日"]!.DataType);
        Assert.Equal(typeof(DateOnly), result.Columns["契約日"]!.DataType);
        Assert.Equal(typeof(DateOnly), result.Columns["入居予定日"]!.DataType);
        Assert.Equal(typeof(DateTime), result.Columns["出力日時"]!.DataType);
        Assert.Equal(typeof(decimal), result.Columns["仲介手数料"]!.DataType); // Added decimal type assertion

        // Verify nullable columns allow DBNull (all properties except Id are nullable in ContractCreation)
        Assert.True(result.Columns["物件No"]!.AllowDBNull);
        Assert.True(result.Columns["部屋No"]!.AllowDBNull);
        Assert.True(result.Columns["契約者1No"]!.AllowDBNull);
        Assert.True(result.Columns["契約者名"]!.AllowDBNull);
        Assert.True(result.Columns["進捗管理ステータス"]!.AllowDBNull);
        Assert.True(result.Columns["契約ステータス"]!.AllowDBNull);
        Assert.True(result.Columns["入居申込日"]!.AllowDBNull);
        Assert.True(result.Columns["契約日"]!.AllowDBNull);
        Assert.True(result.Columns["入居予定日"]!.AllowDBNull);
        Assert.True(result.Columns["出力日時"]!.AllowDBNull);
        Assert.True(result.Columns["仲介手数料"]!.AllowDBNull); // Added nullable check for BrokerageFee

    }

 
}