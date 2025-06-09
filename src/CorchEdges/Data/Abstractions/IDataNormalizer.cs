﻿using System.Data;

namespace CorchEdges.Data.Abstractions;

/// <summary>
/// Provides functionality for normalizing data types within a DataTable to match the expected schema of a target table.
/// </summary>
public interface IDataNormalizer
{
    /// <summary>
    /// Normalizes the column types of a given source DataTable by converting data to desired types
    /// and mapping it to a new DataTable with the target structure.
    /// </summary>
    /// <param name="targetTableName">The name of the output DataTable after normalization.</param>
    /// <param name="sourceTable">The source DataTable containing data to be normalized.</param>
    /// <returns>A new DataTable with normalized column types and data, having the specified target table name.</returns>
    DataTable NormalizeTypes(string targetTableName, DataTable sourceTable);
}