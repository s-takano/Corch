namespace CorchEdges.Data.Abstractions;

public interface ITableNameMapper
{
    string MapTableName(string originalTableName);
}