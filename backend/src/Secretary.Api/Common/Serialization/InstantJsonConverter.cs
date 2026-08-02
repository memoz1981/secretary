using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace Secretary.Api.Serialization;

public sealed class InstantJsonConverter : JsonConverter<Instant>
{
    private static readonly InstantPattern Pattern = InstantPattern.ExtendedIso;

    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString()
            ?? throw new JsonException("Expected an ISO 8601 instant string.");
        var result = Pattern.Parse(text);
        if (!result.Success)
        {
            throw new JsonException($"Invalid instant '{text}': {result.Exception.Message}");
        }

        return result.Value;
    }

    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
        => writer.WriteStringValue(Pattern.Format(value));
}
