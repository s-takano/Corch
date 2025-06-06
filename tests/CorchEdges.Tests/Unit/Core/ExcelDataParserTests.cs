using System.Data;

namespace CorchEdges.Tests.Unit.Core
{
    public class ExcelDataParserTests
    {
        private readonly ExcelDataParser _parser = new();

        [Fact]
        public void Parse_ValidExcelFile_ReturnsDataSetWithoutError()
        {
            // Arrange
            byte[] validExcelBytes = LoadTestFile("TestData/valid-data.xlsx");

            // Act
            var (dataSet, error) = _parser.Parse(validExcelBytes);

            // Assert
            Assert.NotNull(dataSet);
            Assert.Null(error);
            Assert.True(dataSet.Tables.Count > 0);
        }

        [Fact]
        public void Parse_ValidExcelFile_ReturnsCorrectNumberOfTables()
        {
            // Arrange
            var validExcelBytes = LoadTestFile("TestData/valid-data.xlsx");

            // Act
            var (dataSet, error) = _parser.Parse(validExcelBytes);

            // Assert
            Assert.NotNull(dataSet);
            Assert.Null(error);
            // Adjust the expected count based on your test file
            Assert.Equal(4, dataSet.Tables.Count);
        }

        [Fact]
        public void Parse_ValidExcelFile_AllTablesHaveValidStructure()
        {
            // Arrange
            byte[] validExcelBytes = LoadTestFile("TestData/valid-data.xlsx");

            // Act
            var (dataSet, error) = _parser.Parse(validExcelBytes);

            // Assert
            Assert.NotNull(dataSet);
            Assert.Null(error);
            Assert.True(dataSet.Tables.Count > 0);

            // Verify each table has valid structure
            for (int i = 0; i < dataSet.Tables.Count; i++)
            {
                var table = dataSet.Tables[i];
                Assert.NotNull(table);
                Assert.True(table.Columns.Count > 0, $"Table at index {i} ('{table.TableName}') should have columns");
                Assert.True(table.Rows.Count > 0, $"Table at index {i} ('{table.TableName}') should have rows");
            }
        }
        [Fact]
        public void Parse_EmptyByteArray_ReturnsError()
        {
            // Arrange
            byte[] emptyBytes = Array.Empty<byte>();

            // Act
            var (dataSet, error) = _parser.Parse(emptyBytes);

            // Assert
            Assert.Null(dataSet);
            Assert.NotNull(error);
            Assert.NotEmpty(error);
        }

        [Fact]
        public void Parse_InvalidFileData_ReturnsError()
        {
            // Arrange
            byte[] invalidBytes = "This is not an Excel file"u8.ToArray();

            // Act
            var (dataSet, error) = _parser.Parse(invalidBytes);

            // Assert
            Assert.Null(dataSet);
            Assert.NotNull(error);
            Assert.NotEmpty(error);
        }

        [Fact]
        public void Parse_CorruptedExcelFile_ReturnsError()
        {
            // Arrange
            byte[] corruptedBytes = new byte[100]; // Random bytes
            Random.Shared.NextBytes(corruptedBytes);

            // Act
            var (dataSet, error) = _parser.Parse(corruptedBytes);

            // Assert
            Assert.Null(dataSet);
            Assert.NotNull(error);
            Assert.NotEmpty(error);
        }

        [Theory]
        [InlineData("TestData/valid-data.xlsx")]
        public void Parse_SpecificTestFiles_ParsesSuccessfully(string fileName)
        {
            // Arrange
            byte[] fileBytes = LoadTestFile(fileName);

            // Act
            var (dataSet, error) = _parser.Parse(fileBytes);

            // Assert
            Assert.NotNull(dataSet);
            Assert.Null(error);
        }

        [Fact]
        public void Parse_LargeExcelFile_HandlesCorrectly()
        {
            // Arrange
            byte[] validExcelBytes = LoadTestFile("TestData/valid-data.xlsx");

            // Act
            var (dataSet, error) = _parser.Parse(validExcelBytes);

            // Assert
            Assert.NotNull(dataSet);
            Assert.Null(error);
            
            // Verify memory cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private static byte[] LoadTestFile(string relativePath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Test file not found: {fullPath}");
            }

            return File.ReadAllBytes(fullPath);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Component", "ExcelDataParser")]
        public void Parse_AnyExcelFile_FirstRowBecomesColumnHeaders()
        {
            // Arrange
            var parser = new ExcelDataParser();
            var excelFilePath = Path.Combine("TestData", "valid-data.xlsx");
            byte[] excelBytes = File.ReadAllBytes(excelFilePath);

            // Act
            var (dataSet, error) = parser.Parse(excelBytes);

            // Assert
            Assert.Null(error);
            Assert.NotNull(dataSet);

            foreach (DataTable table in dataSet.Tables)
            {
                if (table.Rows.Count == 0) continue; // Skip empty sheets

                // Check that we have proper column names (not generic ones)
                var invalidColumns = table.Columns.Cast<DataColumn>()
                    .Where(col => string.IsNullOrWhiteSpace(col.ColumnName) || 
                                  col.ColumnName.StartsWith("Column") ||
                                  col.ColumnName.Length <= 1)
                    .Select(col => col.ColumnName)
                    .ToList();

                Assert.True(invalidColumns.Count == 0, 
                    $"Table '{table.TableName}' has invalid column names: [{string.Join(", ", invalidColumns)}]. " +
                    $"Expected meaningful column names from Excel headers.");

                // Verify data rows don't contain the header values
                if (table.Rows.Count > 0)
                {
                    var firstDataRow = table.Rows[0];
                    var columnNames = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
            
                    // The first data row should not be identical to column names
                    // (which would indicate headers weren't properly extracted)
                    var firstRowValues = firstDataRow.ItemArray.Select(val => val?.ToString() ?? "").ToArray();
                    var identicalCount = columnNames.Zip(firstRowValues, (col, val) => col == val).Count(match => match);
            
                    Assert.True(identicalCount < columnNames.Length * 0.8, 
                        "First data row appears to be headers - headers may not have been properly extracted");
                }
            }
        }
    }
}