namespace BUTR.NexusModsStats.Utils;

public static class HttpUtils
{
    public static string UserAgent { get; } = CreateUserAgent();

    private static string CreateUserAgent()
    {
        var assemblyName = typeof(HttpUtils).Assembly.GetName();
        return $"{assemblyName.Name ?? "ERROR"} v{assemblyName.Version?.ToString() ?? "ERROR"} (github.com/BUTR)";
    }
}