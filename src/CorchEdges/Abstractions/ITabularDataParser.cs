using System.Data;

namespace CorchEdges.Abstractions;

/// <summary>
/// Represents an abstraction for parsing and converting tabular data from input streams into a structured format.
/// </summary>
/// <remarks>
/// This interface is implemented to process streams (such as Excel file streams) and
/// transform them into a <see cref="System.Data.DataSet"/> while optionally providing
/// error details as a string if the parsing fails or encounters issues.
/// </remarks>
public interface ITabularDataParser
{
    /// Parses a stream, extracting its contents into a DataSet, or returns an error message if parsing fails.
    /// <param name="stream">The input stream containing the Excel file data to be parsed.</param>
    /// <returns>
    /// A tuple containing:
    /// - A DataSet object populated with data from the stream, or null if parsing fails.
    /// - A string containing an error message if parsing fails, or null if parsing succeeds.
    /// </returns>
    (DataSet?, string?) Parse(Stream stream);
}