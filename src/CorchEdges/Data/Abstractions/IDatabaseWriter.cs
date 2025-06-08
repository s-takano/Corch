using System.Data;
using System.Data.Common;

namespace CorchEdges.Data.Abstractions;

public interface IDatabaseWriter
{
    Task WriteAsync(DataSet tables, EdgesDbContext context, DbConnection connection, DbTransaction transaction);
}