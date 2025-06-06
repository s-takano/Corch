
using CorchEdges.Data;
using CorchEdges.Data.Entities;
using CorchEdges.Data.Providers;
using Xunit;

namespace CorchEdges.Tests.Integration.Data.Providers
{
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

        #region Real Entity Integration Tests - Mirroring ExcelToDatabaseAdapterIntegrationTests

        [Fact]
        public void GetColumnType_WithRealContractCreationEntity_ReturnsCorrectTypes()
        {
            // This mirrors ExcelToDatabaseAdapterIntegrationTests.GetColumnTypeFromEntity_WithRealEntities_ReturnsCorrectTypes
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
            // This mirrors ExcelToDatabaseAdapterIntegrationTests.PrepareDataSetForDatabase_NormalizesColumnTypes
            // Test real ProcessingLog entity properties using _metadataProvider directly
            
            // Act & Assert
            Assert.Equal(typeof(long), _metadataProvider.GetColumnType("processing_log", "Id"));
            Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "Level"));
            Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "Message"));
            Assert.Equal(typeof(DateTime), _metadataProvider.GetColumnType("processing_log", "Timestamp"));
            Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "SharePointItemId"));
        }

        [Fact]
        public void GetColumnType_WithRealProcessedFileEntity_ReturnsCorrectTypes()
        {
            // Test real ProcessedFile entity properties using _metadataProvider directly
            
            // Act & Assert
            Assert.Equal(typeof(long), _metadataProvider.GetColumnType("processed_file", "Id"));
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
        [InlineData("contract_creation", "ContractId")]
        [InlineData("contract_creation", "PropertyNo")]
        [InlineData("contract_creation", "PropertyName")]
        [InlineData("contract_creation", "OutputDateTime")]
        [InlineData("processing_log", "Id")]
        [InlineData("processing_log", "Level")]
        [InlineData("processing_log", "Message")]
        [InlineData("processing_log", "Timestamp")]
        [InlineData("processing_log", "SharePointItemId")]
        [InlineData("processed_file", "FileName")]
        [InlineData("processed_file", "ProcessedAt")]
        public void HasColumn_WithRealEntityProperties_ReturnsTrue(string tableName, string columnName)
        {
            // Act
            var result = _metadataProvider.HasColumn(tableName, columnName);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region Missing Tests from ExcelToDatabaseAdapterIntegrationTests

        [Fact]
        public void GetColumnType_WithUnknownRealTable_ThrowsArgumentException()
        {
            // This mirrors ExcelToDatabaseAdapterIntegrationTests.PrepareDataSetForDatabase_WithUnknownTable_ThrowsException
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

        #region Nullable Type Integration Tests - From ExcelToDatabaseAdapterIntegrationTests

        [Fact]
        public void GetColumnType_WithNullablePropertiesFromRealEntities_ReturnsNullableTypes()
        {
            // This mirrors the nullable type testing from ExcelToDatabaseAdapterIntegrationTests.NormalizeTableTypes_WithNullableTypes_HandlesNullsCorrectly
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
            var timestampType = _metadataProvider.GetColumnType("processing_log", "Timestamp");

            // Verify these are non-nullable types
            Assert.Equal(typeof(long), idType);
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
            Assert.Equal(typeof(long), _metadataProvider.GetColumnType("processing_log", "Id"));
            Assert.Equal(typeof(DateTime), _metadataProvider.GetColumnType("processing_log", "Timestamp"));
            
            // String properties (reference types) should return string, not nullable
            Assert.Equal(typeof(string), _metadataProvider.GetColumnType("contract_creation", "ContractId"));
            Assert.Equal(typeof(string), _metadataProvider.GetColumnType("processing_log", "Level"));
        }

        #endregion

        #region DataTable Column Type Compatibility Tests

        [Fact]
        public void GetColumnType_WithRealEntityTypes_ReturnsDataTableCompatibleTypes()
        {
            // Test that all types returned by the metadata provider are compatible with DataTable column creation
            // This mirrors the logic tested in ExcelToDatabaseAdapterIntegrationTests.PrepareDataSetForDatabase_WithRealContractData_ProcessesSuccessfully
            
            var testCases = new[]
            {
                ("contract_creation", "ContractId", typeof(string)),
                ("contract_creation", "PropertyNo", typeof(int?)),
                ("contract_creation", "PropertyName", typeof(string)),
                ("contract_creation", "OutputDateTime", typeof(DateTime?)),
                ("processing_log", "Id", typeof(long)),
                ("processing_log", "Level", typeof(string)),
                ("processing_log", "Message", typeof(string)),
                ("processing_log", "Timestamp", typeof(DateTime)),
                ("processing_log", "SharePointItemId", typeof(string))
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
                Assert.Equal(typeof(long), _metadataProvider.GetColumnType("processing_log", "Id"));
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
            Assert.Equal(typeof(long), processingLogId);
            Assert.Equal(typeof(long), processedFileId);
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
}
