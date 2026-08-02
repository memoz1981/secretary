using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Secretary.Infrastructure.Persistence.Converters;

/// <summary>Converts NodaTime Instant to/from a DateTime (UTC) column — not DateTimeOffset,
/// even though Instant historically gets mapped to datetimeoffset in some NodaTime/EF Core
/// examples. An Instant is unambiguous UTC with no offset to preserve, and the EF Core
/// SQLite provider (used in Infrastructure.Tests) cannot translate ordering or range
/// comparisons on DateTimeOffset at all — confirmed by actually running these queries
/// against SQLite, not by reading the docs. DateTime (UTC) has full comparison/ordering
/// support on both SQL Server and SQLite.</summary>
public sealed class InstantConverter : ValueConverter<Instant, DateTime>
{
    public InstantConverter()
        : base(
            instant => instant.ToDateTimeUtc(),
            dateTime => Instant.FromDateTimeUtc(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)))
    {
    }
}
