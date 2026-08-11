using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace Secretary.Api.Serialization;

/// <summary>A calendar day as "2026-08-11".
///
/// Without this, System.Text.Json reflects over LocalDate and finds Calendar, whose own MinDate
/// and MaxDate are LocalDates with Calendars of their own — the writer recurses until it throws
/// and the response dies half-written. The client sees malformed JSON and a page that will not
/// load, with nothing in the server log above Information. That is exactly how the Orders page
/// failed: the query ran fine ten times over.</summary>
public sealed class LocalDateJsonConverter : JsonConverter<LocalDate>
{
    private static readonly LocalDatePattern Pattern = LocalDatePattern.Iso;

    public override LocalDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("Expected a date like 2026-08-11.");
        var result = Pattern.Parse(text);
        return result.Success
            ? result.Value
            : throw new JsonException($"Invalid date '{text}': {result.Exception.Message}");
    }

    public override void Write(Utf8JsonWriter writer, LocalDate value, JsonSerializerOptions options)
        => writer.WriteStringValue(Pattern.Format(value));
}

/// <summary>A time of day as "09:00" — the format an &lt;input type="time"&gt; reads and writes,
/// so opening hours survive a round trip through the Administration page without translation on
/// either side. Seconds are accepted on the way in and dropped on the way out; nobody opens a
/// shop at 09:00:30.</summary>
public sealed class LocalTimeJsonConverter : JsonConverter<LocalTime>
{
    private static readonly LocalTimePattern HoursAndMinutes = LocalTimePattern.CreateWithInvariantCulture("HH:mm");
    private static readonly LocalTimePattern WithSeconds = LocalTimePattern.CreateWithInvariantCulture("HH:mm:ss");

    public override LocalTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("Expected a time like 09:00.");

        var result = HoursAndMinutes.Parse(text);
        if (result.Success)
        {
            return result.Value;
        }

        var withSeconds = WithSeconds.Parse(text);
        return withSeconds.Success
            ? withSeconds.Value
            : throw new JsonException($"Invalid time '{text}': {result.Exception.Message}");
    }

    public override void Write(Utf8JsonWriter writer, LocalTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(HoursAndMinutes.Format(value));
}
