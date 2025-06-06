namespace CorchEdges.Data.Abstractions;

public interface IEntityMetadataProvider
{
    Type GetColumnType(string tableName, string columnName);
    bool HasTable(string tableName);
    bool HasColumn(string tableName, string columnName);
}