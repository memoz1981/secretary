using NodaTime;
using NodaTime.Text;

namespace Secretary.Agents;

/// <summary>Every caller-facing time is Azerbaijan local time (Asia/Baku), hardcoded by product
/// decision: the platform's callers are in Azerbaijan, and asking the model to convert between
/// UTC and local mid-call is exactly how a caller who asked for 13:00 got a confirmation for
/// 17:00. Tools accept and emit local time ONLY; converting to/from the UTC Instants the
/// database stores happens here and nowhere else.</summary>
public static class AzerbaijanTime
{
    public static readonly DateTimeZone Zone = DateTimeZoneProviders.Tzdb["Asia/Baku"];

    private static readonly LocalDateTimePattern[] InputPatterns =
    [
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm:ss"),
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm"),
    ];

    private static readonly LocalDateTimePattern OutputPattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm");

    private static readonly LocalTimePattern TimeOnlyPattern =
        LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    /// <summary>Accepts "2026-08-01 14:00", with or without seconds, 'T' or space, and forgives
    /// a stray trailing Z (the model's old UTC habit) by ignoring it — the value is always read
    /// as Azerbaijan local time regardless.</summary>
    public static bool TryParse(string localTimeText, out Instant instant)
    {
        var text = localTimeText.Trim().Replace('T', ' ').TrimEnd('Z', 'z').TrimEnd();
        foreach (var pattern in InputPatterns)
        {
            var result = pattern.Parse(text);
            if (result.Success)
            {
                instant = Zone.AtLeniently(result.Value).ToInstant();
                return true;
            }
        }

        instant = default;
        return false;
    }

    public static string Format(Instant instant) => OutputPattern.Format(instant.InZone(Zone).LocalDateTime);

    /// <summary>Just the clock time — used for the end of an availability range whose date was
    /// already stated ("2026-07-26 09:00–21:00").</summary>
    public static string FormatTimeOnly(Instant instant) => TimeOnlyPattern.Format(instant.InZone(Zone).TimeOfDay);

    public static LocalDateTime ToLocal(Instant instant) => instant.InZone(Zone).LocalDateTime;
}
