using CorchEdges.Data.Entities;
using CorchEdges.Data.Providers;
using CorchEdges.Tests.Infrastructure;

namespace CorchEdges.Tests.Functional.Data.Providers;

[Trait("Category", TestCategories.Functional)]
[Trait("Target", "ReflectionEntityMetadataProvider")]
public class ReflectionEntityMetadataProviderFunctionalTests : MemoryDatabaseTestBase
{
    private readonly ReflectionEntityMetadataProvider _metadataProvider = new();

    #region Real Entity Integration Tests - Mirroring ExcelToDatabaseConverterIntegrationTests

    [Fact]
    public void GetColumnType_WithRealContractCreationEntity_ReturnsCorrectTypes()
    {
        // This mirrors ExcelToDatabaseConverterIntegrationTests.GetColumnTypeFromEntity_WithRealEntities_ReturnsCorrectTypes
        // Test real ContractCreation entity properties using _metadataProvider directly
            
        // Act & Assert - Test various property types from real entity
        Assert.Equal(typeof(string), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "ContractId"));
        Assert.Equal(typeof(int?), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "PropertyNo"));
        Assert.Equal(typeof(string), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "PropertyName"));
        Assert.Equal(typeof(DateTime?), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "OutputDateTime"));
    }


    [Theory]
    [InlineData("corch_edges_raw.contract_creation")]
    [InlineData("corch_edges_raw.contract_current")]
    [InlineData("corch_edges_raw.contract_renewal")]
    [InlineData("corch_edges_raw.contract_termination")]
    public void HasTable_WithRealEntityMappings_ReturnsTrue(string tableName)
    {
        // Act
        var result = _metadataProvider.HasEntity(tableName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("corch_edges_raw.contract_creation", "Id")]
    [InlineData("corch_edges_raw.contract_creation", "ContractId")]
    [InlineData("corch_edges_raw.contract_current", "Id")]
    public void HasColumn_WithRealEntityProperties_ReturnsTrue(string tableName, string columnName)
    {
        // Act & Assert
        Assert.True(_metadataProvider.HasProperty(tableName, columnName));
    }

    #endregion

    #region Missing Tests from ExcelToDatabaseConverterIntegrationTests

    [Fact]
    public void GetColumnType_WithUnknownRealTable_ThrowsArgumentException()
    {
        // This mirrors ExcelToDatabaseConverterIntegrationTests.PrepareDataSetForDatabase_WithUnknownTable_ThrowsException
        // but tests the metadata provider directly
            
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _metadataProvider.GetPropertyType("UnknownTableName", "SomeColumn"));

        Assert.Contains("No entity mapping found for table 'UnknownTableName'", exception.Message);
    }

    [Fact]
    public void GetColumnType_WithRealEntityInvalidColumn_ThrowsInvalidOperationException()
    {
        // Test with real entity but invalid column
            
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "NonExistentProperty"));

        Assert.Contains("Column 'NonExistentProperty' not found in entity 'ContractCreation'", exception.Message);
        Assert.Contains("Column names must match entity property names exactly", exception.Message);
        Assert.Contains("Available properties:", exception.Message);
    }

    #endregion

    #region Nullable Type Integration Tests - From ExcelToDatabaseConverterIntegrationTests

    [Fact]
    public void GetColumnType_WithNullablePropertiesFromRealEntities_ReturnsNullableTypes()
    {
        // This mirrors the nullable type testing from ExcelToDatabaseConverterIntegrationTests.NormalizeTableTypes_WithNullableTypes_HandlesNullsCorrectly
        // Test that nullable properties in real entities return nullable types correctly
            
        // Act & Assert - Test nullable properties specifically
        var propertyNoType = _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "PropertyNo");
        var outputDateTimeType = _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "OutputDateTime");

        // Verify these are actually nullable types
        Assert.Equal(typeof(int?), propertyNoType);
        Assert.Equal(typeof(DateTime?), outputDateTimeType);
            
        // Verify they're recognized as nullable (this is the CRITICAL behavior)
        Assert.True(Nullable.GetUnderlyingType(propertyNoType) != null);
        Assert.True(Nullable.GetUnderlyingType(outputDateTimeType) != null);
    }

    [Fact]
    public void GetColumnType_WithNonNullablePropertiesFromRealEntities_ReturnsNonNullableTypes()
    {
        // Test that non-nullable properties return the exact types
            
        // Act & Assert
        var idType = _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "Id");

        // Verify these are non-nullable types
        Assert.Equal(typeof(int), idType);
            
        // Verify they're NOT nullable
        Assert.True(Nullable.GetUnderlyingType(idType) == null);
    }

    [Fact]
    public void GetColumnType_ReturnsExactPropertyType_IncludingNullableWrappers()
    {
        // This tests the CRITICAL comment in ReflectionEntityMetadataProvider:
        // "CRITICAL: Return the ACTUAL property type (including nullable wrappers)"
        // This behavior is essential for the data normalization that follows
            
        // Test that we get the EXACT property types, not the underlying types
        // This is what allows the downstream DataTable creation to set AllowDBNull correctly
            
        // Act & Assert - Nullable properties should return nullable types
        Assert.Equal(typeof(int?), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "PropertyNo"));
        Assert.Equal(typeof(DateTime?), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "OutputDateTime"));
            
        // Non-nullable properties should return non-nullable types
        Assert.Equal(typeof(int), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "Id"));
            
        // String properties (reference types) should return string, not nullable
        Assert.Equal(typeof(string), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "ContractId"));
    }

    #endregion

    #region DataTable Column Type Compatibility Tests

    [Fact]
    public void GetColumnType_WithRealEntityTypes_ReturnsDataTableCompatibleTypes()
    {
        // Test that all types returned by the metadata provider are compatible with DataTable column creation
        // This mirrors the logic tested in ExcelToDatabaseConverterIntegrationTests.PrepareDataSetForDatabase_WithRealContractData_ProcessesSuccessfully
            
        var testCases = new[]
        {
            ("corch_edges_raw.contract_creation", "ContractId", typeof(string)),
            ("corch_edges_raw.contract_creation", "PropertyNo", typeof(int?)),
            ("corch_edges_raw.contract_creation", "RoomNo", typeof(int?)),
            ("corch_edges_raw.contract_creation", "ContractorNo", typeof(int?)),
            ("corch_edges_raw.contract_creation", "PropertyName", typeof(string)),
            ("corch_edges_raw.contract_creation", "ContractorName", typeof(string)),
            ("corch_edges_raw.contract_creation", "ProgressStatus", typeof(string)),
            ("corch_edges_raw.contract_creation", "ContractStatus", typeof(string)),
            ("corch_edges_raw.contract_creation", "ApplicationDate", typeof(DateOnly?)),
            ("corch_edges_raw.contract_creation", "MoveInDate", typeof(DateOnly?)),
            ("corch_edges_raw.contract_creation", "KeyHandoverDate", typeof(DateOnly?)),
            ("corch_edges_raw.contract_creation", "ContractDate", typeof(DateOnly?)),
            ("corch_edges_raw.contract_creation", "KeyMoney", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "SecurityDeposit", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "BrokerageFee", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "GuaranteeFee", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "ApartmentInsurance", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "KeyReplacementFee", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "DocumentStampFee", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "WithdrawalFee", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "BicycleRegistrationFee", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "MotorcycleRegistrationFee", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "InternetApplicationFee", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "MaximumAmount", typeof(decimal?)),
            ("corch_edges_raw.contract_creation", "OutputDateTime", typeof(DateTime?))
        };
        
        foreach (var (tableName, columnName, expectedType) in testCases)
        {
            // Act
            var actualType = _metadataProvider.GetPropertyType(tableName, columnName);
                
            // Assert
            Assert.Equal(expectedType, actualType);
                
            // Verify the type is DataTable compatible
            var underlyingType = Nullable.GetUnderlyingType(actualType) ?? actualType;
            Assert.True(IsDataTableCompatibleType(underlyingType), 
                $"Type {actualType} for {tableName}.{columnName} should be DataTable compatible");
        }
    }

    #endregion

    #region Reflection Integration Tests

    [Fact]
    public void GetColumnType_WithRealEntityInheritance_HandlesBaseClassProperties()
    {
        // If any of the real entities inherit from base classes, this tests that inheritance works
        // For now, just verify that the entities work correctly with reflection
            
        // Act
        var contractCreationType = typeof(ContractCreation);
        var properties = contractCreationType.GetProperties();

        // Assert
        Assert.True(properties.Length > 0, "ContractCreation should have properties");
            
        // Verify that the metadata provider can access all these properties
        foreach (var property in properties)
        {
            // This should not throw for any public property
            var columnType = _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", property.Name);
            Assert.Equal(property.PropertyType, columnType);
        }
    }

    [Fact]
    public void GetColumnType_WithComplexRealEntityTypes_HandlesAllPropertyTypes()
    {
        // Test various data types that might exist in real entities
            
        // Get all properties from ProcessingLog (likely to have diverse types)
        var entityType = typeof(ContractCreation);
        var properties = entityType.GetProperties().Where(p=>IsDataConvertibleType(p.PropertyType));

        foreach (var property in properties)
        {
            // Act
            var columnType = _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", property.Name);

            // Assert - The returned type should exactly match the property type
            Assert.Equal(property.PropertyType, columnType);
                
            // Verify the type is one we can handle in data conversion
            Assert.True(IsDataConvertibleType(columnType), 
                $"Property {property.Name} has type {columnType} which should be data-convertible");
        }
    }

    #endregion

    #region Performance Integration Tests

    [Fact]
    public void GetColumnType_WithRepeatedCallsOnRealEntities_PerformsConsistently()
    {
        // Test that repeated calls return consistent results (no caching issues)
            
        // Act & Assert - Multiple calls should return identical results
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(typeof(string), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "ContractId"));
            Assert.Equal(typeof(int?), _metadataProvider.GetPropertyType("corch_edges_raw.contract_creation", "PropertyNo"));
        }
    }


    #endregion

    #region Helper Methods

    private static bool IsDataConvertibleType(Type type)
    {
        // Check if the type is something we can reasonably convert data to/from
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return IsDataTableCompatibleType(underlyingType);
    }

    private static bool IsDataTableCompatibleType(Type type)
    {
        // DataTable supports these types directly
        return type == typeof(string) ||
               type == typeof(int) ||
               type == typeof(long) ||
               type == typeof(decimal) ||
               type == typeof(double) ||
               type == typeof(bool) ||
               type == typeof(DateTime) ||
               type == typeof(DateOnly) ||
               type == typeof(TimeOnly) ||
               type.IsEnum;
    }

    #endregion
}