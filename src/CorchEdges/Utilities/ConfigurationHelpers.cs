namespace CorchEdges.Utilities;

public static class ConfigurationHelpers
{
    public static string? GetConfigValue(this Dictionary<string, object>? requestData, string key)
    {
        if (requestData != null && requestData.ContainsKey(key))
        {
            return requestData[key]?.ToString();
        }
        return null;
    }
}