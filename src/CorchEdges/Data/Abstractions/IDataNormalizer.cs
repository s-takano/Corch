using System.Data;

namespace CorchEdges.Data.Abstractions;

public interface IDataNormalizer
{
    DataTable NormalizeTypes(string targetTableName, DataTable sourceTable);
}