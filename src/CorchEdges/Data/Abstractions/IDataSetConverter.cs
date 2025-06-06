using System.Data;

namespace CorchEdges.Data.Abstractions;

public interface IDataSetConverter
{
    DataSet ConvertForDatabase(DataSet sourceDataSet);
}