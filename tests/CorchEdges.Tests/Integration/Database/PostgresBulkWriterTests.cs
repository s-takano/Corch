using System.Data;
using CorchEdges.Data;
using CorchEdges.Data.Abstractions;
using Npgsql;
using Xunit;

namespace CorchEdges.Tests.Integration.Database;

public class PostgresTableWriterTests : PostgresDatabaseTestBase
{
    protected override string TestSchema { get;  } = "corch_edges_raw";
    
    private readonly IPostgresTableWriter _writer = new PostgresTableWriter();

    [Fact]
    public async Task WriteAsync_SingleTableWithMatchingSchema_InsertsDataSuccessfully()
    {
        // Arrange
        var dataSet = CreateTestDataSet();
        var tableName = await SetupTestTable("employees", 
            "id INTEGER PRIMARY KEY, " +
            "name VARCHAR(100) NOT NULL, " +
            "email VARCHAR(255) UNIQUE, " +
            "salary DECIMAL(10,2), " +
            "hire_date DATE DEFAULT CURRENT_DATE, " +
            "department_id INTEGER, " +
            "is_active BOOLEAN DEFAULT true, " +
            "created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP");
            
        dataSet.Tables[0].TableName = tableName;
            
        await using var transaction = await Connection.BeginTransactionAsync();

        // Act
        await _writer.WriteAsync(dataSet, Connection, transaction);
        await transaction.CommitAsync();

        // Assert
        var count = await GetTableRowCount(tableName);
        Assert.Equal(2, count);
            
        // Verify actual data
        var tableData = await GetTableData(tableName);
        Assert.Equal(2, tableData.Count);
        Assert.Equal("John Doe", tableData[0]["name"]);
        Assert.Equal("jane@example.com", tableData[1]["email"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WriteAsync_MultipleTablesWithMatchingSchema_InsertsAllTablesSuccessfully()
    {
        // Arrange
        var dataSet = CreateMultiTableDataSet();
            
        // Create departments table first (referenced by employees)
        var deptTableName = await SetupTestTable("departments", 
            "dept_id INTEGER PRIMARY KEY, " +
            "dept_name VARCHAR(100) NOT NULL UNIQUE, " +
            "budget DECIMAL(12,2), " +
            "location VARCHAR(50), " +
            "established_date DATE");

        // Create employees table with foreign key reference
        var empTableName = await SetupTestTable("employees", 
            "id INTEGER PRIMARY KEY, " +
            "name VARCHAR(100) NOT NULL, " +
            "email VARCHAR(255) UNIQUE, " +
            "department_id INTEGER, " +
            "salary DECIMAL(10,2) CHECK (salary > 0), " +
            "hire_date DATE, " +
            "is_manager BOOLEAN DEFAULT false");

        // Update table names in dataset to match qualified names
        dataSet.Tables["departments"]!.TableName = deptTableName;
        dataSet.Tables["employees"]!.TableName = empTableName;
            
        await using var transaction = await Connection.BeginTransactionAsync();

        // Act
        await _writer.WriteAsync(dataSet, Connection, transaction);
        await transaction.CommitAsync();

        // Assert
        var empCount = await GetTableRowCount(empTableName);
        var deptCount = await GetTableRowCount(deptTableName);
        
        Assert.Equal(2, empCount);
        Assert.Equal(2, deptCount);
            
        // Verify department data
        var deptData = await GetTableData(deptTableName);
        Assert.Contains(deptData, d => d["dept_name"].ToString() == "Engineering");
        Assert.Contains(deptData, d => d["dept_name"].ToString() == "Marketing");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WriteAsync_EmptyDataSet_CompletesWithoutError()
    {
        // Arrange
        var dataSet = CreateEmptyDataSet();
        var tableName = await SetupTestTable("empty_test", 
            "id INTEGER PRIMARY KEY, " +
            "name VARCHAR(50)");
            
        dataSet.Tables[0].TableName = tableName;
        await using var transaction = await Connection.BeginTransactionAsync();

        // Act & Assert - Should not throw
        await _writer.WriteAsync(dataSet, Connection, transaction);
        await transaction.CommitAsync();
            
        var count = await GetTableRowCount(tableName);
        Assert.Equal(0, count);
    }

    [Fact]
    [Trait("Error", "TableNameValidation")]
    public async Task WriteAsync_InvalidSchemaName_ThrowsArgumentException()
    {
        // Arrange
        var dataSet = CreateTestDataSet();
        dataSet.Tables[0].TableName = ".table"; // Empty schema name (more realistic)
            
        await using var transaction = await Connection.BeginTransactionAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _writer.WriteAsync(dataSet, Connection, transaction));
            
        Assert.Contains("Empty schema name", exception.Message);
    }

    [Fact]
    [Trait("Error", "TableNameValidation")]
    public async Task WriteAsync_InvalidTableName_ThrowsArgumentException()
    {
        // Arrange
        var dataSet = CreateTestDataSet();
        dataSet.Tables[0].TableName = "schema."; // Empty table name (more realistic)
            
        await using var transaction = await Connection.BeginTransactionAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _writer.WriteAsync(dataSet, Connection, transaction));
            
        Assert.Contains("Empty table name", exception.Message);
    }

    // Helper methods for creating test data
    private DataSet CreateTestDataSet()
    {
        var dataSet = new DataSet();
        var table = new DataTable("employees");
        
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Columns.Add("email", typeof(string));
        table.Columns.Add("salary", typeof(decimal));
        table.Columns.Add("department_id", typeof(int));
        table.Columns.Add("is_active", typeof(bool));

        table.Rows.Add(1, "John Doe", "john@example.com", 75000.00m, 1, true);
        table.Rows.Add(2, "Jane Smith", "jane@example.com", 85000.00m, 2, true);

        dataSet.Tables.Add(table);
        return dataSet;
    }

    private DataSet CreateMultiTableDataSet()
    {
        var dataSet = new DataSet();
        
        // Departments table (must be inserted first due to foreign key)
        var deptTable = new DataTable("departments");
        deptTable.Columns.Add("dept_id", typeof(int));
        deptTable.Columns.Add("dept_name", typeof(string));
        deptTable.Columns.Add("budget", typeof(decimal));
        deptTable.Columns.Add("location", typeof(string));
            
        deptTable.Rows.Add(1, "Engineering", 500000.00m, "Building A");
        deptTable.Rows.Add(2, "Marketing", 300000.00m, "Building B");
        
        // Employees table
        var empTable = new DataTable("employees");
        empTable.Columns.Add("id", typeof(int));
        empTable.Columns.Add("name", typeof(string));
        empTable.Columns.Add("email", typeof(string));
        empTable.Columns.Add("department_id", typeof(int));
        empTable.Columns.Add("salary", typeof(decimal));
        empTable.Columns.Add("is_manager", typeof(bool));
            
        empTable.Rows.Add(1, "John Doe", "john@example.com", 1, 75000.00m, false);
        empTable.Rows.Add(2, "Jane Smith", "jane@example.com", 2, 85000.00m, true);

        // Add departments first to satisfy foreign key constraints
        dataSet.Tables.Add(deptTable);
        dataSet.Tables.Add(empTable);
        return dataSet;
    }

    private DataSet CreateEmptyDataSet()
    {
        var dataSet = new DataSet();
        var table = new DataTable("empty_test");

        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));

        // No rows added - empty table
        dataSet.Tables.Add(table);
        return dataSet;
    }
}