using System.Data;

namespace CorchEdges.Abstractions;

/// <summary>
/// Defines a contract for parsing Excel file content into a structured format.
/// </summary>
/// <remarks>
/// This interface is used to process byte array representations of Excel files
/// and transform them into a <see cref="System.Data.DataSet"/> along with
/// any parsing-related error information as a string, if applicable.
/// </remarks>
public interface IExcelParser {
    /// Parses an Excel file represented as a byte array, extracting its contents into a DataSet, or returns an error message if parsing fails.
    /// <param name="stream"></param>
    /// <returns>
    /// A tuple containing:
    /// - A DataSet object containing tables for each valid worksheet in the Excel file, or null if parsing fails.
    /// - A string containing an error message if parsing fails, or null if parsing is successful.
    /// </returns>
    (DataSet?, string?) Parse(Stream stream); 
}