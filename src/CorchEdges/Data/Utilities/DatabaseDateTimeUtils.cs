namespace CorchEdges.Data.Utilities;

/// <summary>
/// Provides database-compatible DateTime values for PostgreSQL timestamp columns.
/// </summary>
public static class DatabaseDateTime
{
    /// <summary>
    /// Gets the current UTC time as DateTime with DateTimeKind.Unspecified.
    /// This is compatible with PostgreSQL 'timestamp without time zone' columns.
    /// </summary>
    public static DateTime UtcNow => DateTimeOffset.UtcNow.DateTime;
    
    /// <summary>
    /// Gets the current UTC time as DateTimeOffset for duration calculations.
    /// </summary>
    public static DateTimeOffset OffsetUtcNow => DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Converts any DateTime to Unspecified kind for database compatibility.
    /// </summary>
    public static DateTime ToUnspecified(DateTime dateTime) 
        => DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
}