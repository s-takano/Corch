using System.Data;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using Moq;

namespace CorchEdges.Tests.Unit.Data;

public class StrictSchemaDetectorTests
{
    private readonly Mock<IEntityMetadataProvider> _metadataProviderMock;
    private readonly StrictSchemaDetector _sut;

    public StrictSchemaDetectorTests()
    {
        _metadataProviderMock = new Mock<IEntityMetadataProvider>();
        // Using default ignored columns: "id", "ProcessedFileId"
        _sut = new StrictSchemaDetector(_metadataProviderMock.Object);
    }

    [Fact]
    public void DetectQualifiedEntityName_WhenHeadersMatchExactly_ReturnsQualifiedName()
    {
        // Arrange
        const string sheetName = "Contracts";
        var table = CreateDataTable(sheetName, "ContractId", "Amount", "Status");

        var metaInfo = CreateMockMetaInfo(sheetName, "raw", "contract_data", 
            ("Id", "id"), // Should be ignored
            ("ContractId", "ContractId"),
            ("Amount", "Amount"),
            ("Status", "Status"));

        SetupMetadataLookup(metaInfo);

        // Act
        var result = _sut.DetectQualifiedEntityName(table);

        // Assert
        Assert.Equal("raw.contract_data", result);
    }

    [Fact]
    public void DetectQualifiedEntityName_WhenNoSchemaProvided_ReturnsOnlyTableName()
    {
        // Arrange
        const string sheetName = "SimpleSheet";
        var table = CreateDataTable(sheetName, "Name");

        var metaInfo = CreateMockMetaInfo(sheetName, string.Empty, "simple_table", 
            ("Name", "Name"));

        SetupMetadataLookup(metaInfo);

        // Act
        var result = _sut.DetectQualifiedEntityName(table);

        // Assert
        Assert.Equal("simple_table", result);
    }

    [Fact]
    public void DetectQualifiedEntityName_WithExtraColumnInExcel_ThrowsArgumentException()
    {
        // Arrange
        const string sheetName = "Contracts";
        // Excel has an extra "Notes" column not in the metadata
        var table = CreateDataTable(sheetName, "ContractId", "Notes");

        var metaInfo = CreateMockMetaInfo(sheetName, "raw", "contracts", 
            ("ContractId", "ContractId"));

        SetupMetadataLookup(metaInfo);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _sut.DetectQualifiedEntityName(table));
        Assert.Contains("No strict schema match found", ex.Message);
    }

    [Fact]
    public void DetectQualifiedEntityName_WithMissingColumnInExcel_ThrowsArgumentException()
    {
        // Arrange
        const string sheetName = "Contracts";
        // Excel is missing "Amount" which is expected by metadata
        var table = CreateDataTable(sheetName, "ContractId");

        var metaInfo = CreateMockMetaInfo(sheetName, "raw", "contracts", 
            ("ContractId", "ContractId"),
            ("Amount", "Amount"));

        SetupMetadataLookup(metaInfo);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _sut.DetectQualifiedEntityName(table));
    }

    [Fact]
    public void DetectQualifiedEntityName_IsCaseInsensitiveForHeaders()
    {
        // Arrange
        const string sheetName = "Contracts";
        // Excel has lowercase, Metadata has PascalCase
        var table = CreateDataTable(sheetName, "contractid", "amount");

        var metaInfo = CreateMockMetaInfo(sheetName, "raw", "contracts", 
            ("ContractId", "ContractId"),
            ("Amount", "Amount"));

        SetupMetadataLookup(metaInfo);

        // Act
        var result = _sut.DetectQualifiedEntityName(table);

        // Assert
        Assert.Equal("raw.contracts", result);
    }

    [Fact]
    public void DetectQualifiedEntityName_WhenMultipleConfigsExist_ReturnsCorrectOneBasedOnHeaders()
    {
        // Arrange
        const string sheetName = "SharedSheetName";
        var table = CreateDataTable(sheetName, "UniqueA");

        var configA = CreateMockMetaInfo(sheetName, "dbo", "TableA", ("PropA", "UniqueA"));
        var configB = CreateMockMetaInfo(sheetName, "dbo", "TableB", ("PropB", "UniqueB"));

        SetupMetadataLookup(configA, configB);

        // Act
        var result = _sut.DetectQualifiedEntityName(table);

        // Assert
        Assert.Equal("dbo.TableA", result);
    }

    private static DataTable CreateDataTable(string name, params string[] columns)
    {
        var table = new DataTable(name);
        foreach (var col in columns) table.Columns.Add(col);
        return table;
    }

    private static IEntityTypeMetaInfo CreateMockMetaInfo(string sheetName, string schema, string table, params (string prop, string col)[] columns)
    {
        var mock = new Mock<IEntityTypeMetaInfo>();
        mock.Setup(m => m.SheetName).Returns(sheetName);
        mock.Setup(m => m.GetSchemaName()).Returns(schema);
        mock.Setup(m => m.GetTableName()).Returns(table);
        mock.Setup(m => m.GetColumnMetadata()).Returns(
            columns.Select(c => new ColumnMetaInfo(c.prop, c.col)).ToList()
        );
        return mock.Object;
    }

    private void SetupMetadataLookup(params IEntityTypeMetaInfo[] configs)
    {
        var lookup = configs.ToLookup(c => c.SheetName);
        _metadataProviderMock.Setup(m => m.GetEntityTypeMetaInfoBySheetName()).Returns(lookup);
    }
}
