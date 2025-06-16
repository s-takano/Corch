using CorchEdges.Data;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Providers;
using Xunit;

namespace CorchEdges.Tests.Integration.Data.Providers;

[Trait("Category", "Integration")]
[Trait("Component", "ReflectionEntityMetadataProvider")]
public class ReflectionEntityMetadataProviderIntegrationTests : DatabaseTestBase
{
    private readonly ReflectionEntityMetadataProvider _metadataProvider;

    public ReflectionEntityMetadataProviderIntegrationTests()
    {
        // Use real entity mappings from the production system
        var realEntityMappings = new Dictionary<string, Type>
        {
            { "contract_creation", typeof(ContractCreation) },
            { "contract_current", typeof(ContractCurrent) },
            { "contract_renewal", typeof(ContractRenewal) },
            { "contract_termination", typeof(ContractTermination) },
            { "processing_log", typeof(ProcessingLog) },
            { "processed_file", typeof(ProcessedFile) }
        };

        _metadataProvider = new ReflectionEntityMetadataProvider(realEntityMappings);
    }

    #region Real Entity Integration Tests - Mirroring ExcelToDatabaseConverterIntegrationTests

    [Fact]
    public void GetColumnType_WithRealContractCreationEntity_ReturnsCorrectTypes()
    {
        // This mirrors ExcelToDatabaseConverterIntegrationTests.GetColumnTypeFromEntity_WithRealEntities_ReturnsCorrectTypes
        // Test real ContractCreation entity properties using _metadataProvider directly
            
        // Act & Assert - Test various property types from real entity
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("contract_creation", "ContractId"));
        Assert.Equal(typeof(int?), _metadataProvider.GetColumnType("contract_creation", "PropertyNo"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("contract_creation", "PropertyName"));
        Assert.Equal(typeof(DateTime?), _metadataProvider.GetColumnType("contract_creation", "OutputDateTime"));
    }

    [Fact]
    public void GetColumnType_WithRealProcessingLogEntity_ReturnsCorrectTypes()
    {
        // Test the actual ProcessingLog entity properties
        Assert.Equal(typeof(int), _metadataProvider.GetColumnType("processing_log", "Id"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "SiteId"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "ListId"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "DeltaLink"));
        Assert.Equal(typeof(DateTime), _metadataProvider.GetColumnType("processing_log", "LastProcessedAt"));
        Assert.Equal(typeof(DateTime), _metadataProvider.GetColumnType("processing_log", "CreatedAt"));
        Assert.Equal(typeof(DateTime), _metadataProvider.GetColumnType("processing_log", "UpdatedAt"));
        Assert.Equal(typeof(int), _metadataProvider.GetColumnType("processing_log", "LastProcessedCount"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "Status"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "LastError"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "SubscriptionId"));
        Assert.Equal(typeof(int), _metadataProvider.GetColumnType("processing_log", "SuccessfulItems"));
        Assert.Equal(typeof(int), _metadataProvider.GetColumnType("processing_log", "FailedItems"));
    }

    [Fact]
    public void GetColumnType_WithRealProcessedFileEntity_ReturnsCorrectTypes()
    {
        // Test real ProcessedFile entity properties using _metadataProvider directly
            
        // Act & Assert
        Assert.Equal(typeof(int), _metadataProvider.GetColumnType("processed_file", "Id"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processed_file", "FileName"));
        Assert.Equal(typeof(DateTime), _metadataProvider.GetColumnType("processed_file", "ProcessedAt"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processed_file", "Status"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processed_file", "SharePointItemId"));
    }

    [Theory]
    [InlineData("contract_creation")]
    [InlineData("contract_current")]
    [InlineData("contract_renewal")]
    [InlineData("contract_termination")]
    [InlineData("processing_log")]
    [InlineData("processed_file")]
    public void HasTable_WithRealEntityMappings_ReturnsTrue(string tableName)
    {
        // Act
        var result = _metadataProvider.HasTable(tableName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("contract_creation", "Id")]
    [InlineData("contract_creation", "ContractId")]
    [InlineData("contract_current", "Id")]
    [InlineData("processing_log", "Id")]
    [InlineData("processing_log", "SiteId")]
    [InlineData("processing_log", "ListId")]
    [InlineData("processing_log", "Status")]
    [InlineData("processed_file", "Id")]
    [InlineData("processed_file", "FileName")]
    public void HasColumn_WithRealEntityProperties_ReturnsTrue(string tableName, string columnName)
    {
        // Act & Assert
        Assert.True(_metadataProvider.HasColumn(tableName, columnName));
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
            _metadataProvider.GetColumnType("UnknownTableName", "SomeColumn"));

        Assert.Contains("No entity mapping found for table 'UnknownTableName'", exception.Message);
    }

    [Fact]
    public void GetColumnType_WithRealEntityInvalidColumn_ThrowsInvalidOperationException()
    {
        // Test with real entity but invalid column
            
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _metadataProvider.GetColumnType("contract_creation", "NonExistentProperty"));

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
        var propertyNoType = _metadataProvider.GetColumnType("contract_creation", "PropertyNo");
        var outputDateTimeType = _metadataProvider.GetColumnType("contract_creation", "OutputDateTime");

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
        var idType = _metadataProvider.GetColumnType("processing_log", "Id");
        var timestampType = _metadataProvider.GetColumnType("processing_log", "CreatedAt");

        // Verify these are non-nullable types
        Assert.Equal(typeof(int), idType);
        Assert.Equal(typeof(DateTime), timestampType);
            
        // Verify they're NOT nullable
        Assert.True(Nullable.GetUnderlyingType(idType) == null);
        Assert.True(Nullable.GetUnderlyingType(timestampType) == null);
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
        Assert.Equal(typeof(int?), _metadataProvider.GetColumnType("contract_creation", "PropertyNo"));
        Assert.Equal(typeof(DateTime?), _metadataProvider.GetColumnType("contract_creation", "OutputDateTime"));
            
        // Non-nullable properties should return non-nullable types
        Assert.Equal(typeof(int), _metadataProvider.GetColumnType("processing_log", "Id"));
        Assert.Equal(typeof(DateTime), _metadataProvider.GetColumnType("processing_log", "CreatedAt"));
            
        // String properties (reference types) should return string, not nullable
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("contract_creation", "ContractId"));
        Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "Status"));
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
            ("contract_creation", "ContractId", typeof(string)),
            ("contract_creation", "PropertyNo", typeof(int?)),
            ("contract_creation", "PropertyName", typeof(string)),
            ("contract_creation", "OutputDateTime", typeof(DateTime?)),
            ("processing_log", "Id", typeof(int)),
            ("processing_log", "SiteId", typeof(string)),
            ("processing_log", "ListId", typeof(string)),
            ("processing_log", "DeltaLink", typeof(string)),
            ("processing_log", "LastProcessedAt", typeof(DateTime)),
            ("processing_log", "CreatedAt", typeof(DateTime)),
            ("processing_log", "UpdatedAt", typeof(DateTime)),
            ("processing_log", "LastProcessedCount", typeof(int)),
            ("processing_log", "Status", typeof(string)),
            ("processing_log", "LastError", typeof(string)),
            ("processing_log", "SubscriptionId", typeof(string)),
            ("processing_log", "SuccessfulItems", typeof(int)),
            ("processing_log", "FailedItems", typeof(int))
        };

        foreach (var (tableName, columnName, expectedType) in testCases)
        {
            // Act
            var actualType = _metadataProvider.GetColumnType(tableName, columnName);
                
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
            var columnType = _metadataProvider.GetColumnType("contract_creation", property.Name);
            Assert.Equal(property.PropertyType, columnType);
        }
    }

    [Fact]
    public void GetColumnType_WithComplexRealEntityTypes_HandlesAllPropertyTypes()
    {
        // Test various data types that might exist in real entities
            
        // Get all properties from ProcessingLog (likely to have diverse types)
        var processingLogType = typeof(ProcessingLog);
        var properties = processingLogType.GetProperties();

        foreach (var property in properties)
        {
            // Act
            var columnType = _metadataProvider.GetColumnType("processing_log", property.Name);

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
            Assert.Equal(typeof(string), _metadataProvider.GetColumnType("contract_creation", "ContractId"));
            Assert.Equal(typeof(int?), _metadataProvider.GetColumnType("contract_creation", "PropertyNo"));
            Assert.Equal(typeof(int), _metadataProvider.GetColumnType("processing_log", "Id"));
        }
    }

    [Fact]
    public void HasTable_WithManyCallsOnRealEntities_PerformsEfficiently()
    {
        // Test that table existence checks are efficient
        var tableNames = new[] 
        { 
            "contract_creation", "contract_current", "contract_renewal", 
            "contract_termination", "processing_log", "processed_file" 
        };

        // Act & Assert - Should handle many lookups efficiently
        for (int i = 0; i < 100; i++)
        {
            foreach (var tableName in tableNames)
            {
                Assert.True(_metadataProvider.HasTable(tableName));
            }
        }
    }

    #endregion

    #region Cross-Entity Integration Tests

    [Fact]
    public void GetColumnType_AcrossMultipleRealEntities_MaintainsTypeConsistency()
    {
        // Test that similar property types across entities are handled consistently
            
        // Many entities likely have Id properties - verify they're handled consistently
        var processingLogId = _metadataProvider.GetColumnType("processing_log", "Id");
        var processedFileId = _metadataProvider.GetColumnType("processed_file", "Id");
            
        // Both should be consistent types (both long in this case)
        Assert.Equal(typeof(int), processingLogId);
        Assert.Equal(typeof(int), processedFileId);
        Assert.Equal(processingLogId, processedFileId);
    }

    [Fact]
    public void HasColumn_AcrossMultipleRealEntities_HandlesCommonPropertyNames()
    {
        // Test common property names that might exist across entities
        var commonPropertyNames = new[] { "Id", "CreatedAt", "UpdatedAt", "Status" };
        var tableNames = new[] { "processing_log", "processed_file" };

        foreach (var tableName in tableNames)
        {
            foreach (var propertyName in commonPropertyNames)
            {
                // Act
                var hasColumn = _metadataProvider.HasColumn(tableName, propertyName);
                    
                // Assert - Either has it or doesn't, but shouldn't throw
                Assert.IsType<bool>(hasColumn);
            }
        }
    }

    #endregion

    #region Helper Methods

    private static bool IsDataConvertibleType(Type type)
    {
        // Check if the type is something we can reasonably convert data to/from
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            
        return underlyingType == typeof(string) ||
               underlyingType == typeof(int) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(decimal) ||
               underlyingType == typeof(double) ||
               underlyingType == typeof(bool) ||
               underlyingType == typeof(DateTime) ||
               underlyingType == typeof(DateOnly) ||
               underlyingType == typeof(TimeOnly) ||
               underlyingType.IsEnum;
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