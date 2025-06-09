using System.Data;

namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Defines a contract for converting a given DataSet to a format suitable for database processing.
/// </summary>
public interface IDataSetConverter
{
    /// <summary>
    /// Converts the given source DataSet into a format suitable for database storage.
    /// This may involve transforming column names, data types, or values,
    /// and filtering or processing tables based on predefined rules.
    /// </summary>
    /// <param name="sourceDataSet">The source DataSet containing the data to be converted for database storage.</param>
    /// <returns>A new DataSet that has been transformed and formatted for compatibility with the target database.</returns>
    DataSet ConvertForDatabase(DataSet sourceDataSet);
}