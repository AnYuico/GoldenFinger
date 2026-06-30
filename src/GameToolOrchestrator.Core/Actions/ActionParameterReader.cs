using System.Globalization;

namespace GameToolOrchestrator.Core.Actions;

public static class ActionParameterReader
{
    public static string? GetString(IReadOnlyDictionary<string, string> parameters, string key)
    {
        foreach (var pair in parameters)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    public static bool GetBool(IReadOnlyDictionary<string, string> parameters, string key, bool defaultValue)
    {
        var value = GetString(parameters, key);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public static double GetDouble(IReadOnlyDictionary<string, string> parameters, string key, double defaultValue)
    {
        var value = GetString(parameters, key);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    public static TimeSpan GetRequiredDuration(
        IReadOnlyDictionary<string, string> parameters,
        double defaultSeconds = 1)
    {
        var milliseconds = GetString(parameters, "milliseconds");
        if (double.TryParse(milliseconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMilliseconds))
        {
            return TimeSpan.FromMilliseconds(parsedMilliseconds);
        }

        var seconds = GetString(parameters, "seconds") ?? GetString(parameters, "durationSeconds");
        if (double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSeconds))
        {
            return TimeSpan.FromSeconds(parsedSeconds);
        }

        return TimeSpan.FromSeconds(defaultSeconds);
    }

    public static TimeSpan? GetTimeout(
        IReadOnlyDictionary<string, string> parameters,
        int? timeoutSeconds,
        int? timeoutMinutes = null)
    {
        var timeoutMilliseconds = GetString(parameters, "timeoutMilliseconds");
        if (double.TryParse(timeoutMilliseconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMilliseconds))
        {
            return parsedMilliseconds > 0 ? TimeSpan.FromMilliseconds(parsedMilliseconds) : null;
        }

        var parameterSeconds = GetString(parameters, "timeoutSeconds");
        if (double.TryParse(parameterSeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSeconds))
        {
            return parsedSeconds > 0 ? TimeSpan.FromSeconds(parsedSeconds) : null;
        }

        var parameterMinutes = GetString(parameters, "timeoutMinutes");
        if (double.TryParse(parameterMinutes, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMinutes))
        {
            return parsedMinutes > 0 ? TimeSpan.FromMinutes(parsedMinutes) : null;
        }

        if (timeoutMinutes is > 0)
        {
            return TimeSpan.FromMinutes(timeoutMinutes.Value);
        }

        return timeoutSeconds is > 0 ? TimeSpan.FromSeconds(timeoutSeconds.Value) : null;
    }
}
