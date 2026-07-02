namespace BUTR.NexusModsStats.Utils;

public static class RequestValidation
{
    private const int MaxIdLength = 64;

    /// <summary>
    /// Accepts numeric ids ("3030") and game domains ("mountandblade2bannerlord"),
    /// rejecting anything that could not be a NexusMods identifier before it reaches an upstream URL.
    /// </summary>
    public static bool IsValidId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxIdLength)
            return false;

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
                return false;
        }
        return true;
    }
}