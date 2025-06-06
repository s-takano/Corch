
namespace CorchEdges.Data.Abstractions;

public interface IColumnNameMapper
{
    string MapColumnName(string originalTableName, string originalColumnName);
    string ValidateColumnName(string columnName);

}