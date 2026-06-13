using Microsoft.Extensions.Logging;

namespace Portal.Models;

public sealed record AppSettings
{
    public LogLevel LoggingMinLevel { get; private init; } = LogLevel.Information;

    public static AppSettings From(IReadOnlyDictionary<string, string> raw)
    {
        var s = new AppSettings();
        if (raw.TryGetValue("Logging.MinLevel", out var lvl))
        {
            if (Enum.TryParse<LogLevel>(lvl, ignoreCase: true, out var parsed))
                s = s with { LoggingMinLevel = parsed };
            else if (int.TryParse(lvl, out var i) && Enum.IsDefined(typeof(LogLevel), i))
                s = s with { LoggingMinLevel = (LogLevel)i };
        }
        return s;
    }
}
