
namespace CorchEdges.Data.Abstractions;

public interface IColumnNameMapper
{
    string MapColumnName(string originalTableName, string originalColumnName);
    
    // todo: remove because it violates the interface segregation principle
    string ValidateColumnName(string columnName);

}