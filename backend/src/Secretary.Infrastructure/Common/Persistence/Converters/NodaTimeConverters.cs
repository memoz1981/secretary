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

/// <summary>A calendar day with no time and no zone, for the one thing that genuinely is one:
/// the day a caller asks their order to arrive. Storing that as an Instant would invent a
/// midnight and then a timezone to interpret it in, and "tomorrow" would land on the wrong day
/// for anyone reading it in UTC.
///
/// DateTime with Kind Unspecified rather than the provider's own date type, so the same model
/// runs on SQL Server, PostgreSQL and the SQLite used in tests.</summary>
public sealed class LocalDateConverter : ValueConverter<LocalDate, DateTime>
{
    public LocalDateConverter()
        : base(
            date => date.ToDateTimeUnspecified(),
            dateTime => LocalDate.FromDateTime(dateTime))
    {
    }
}

/// <summary>A time of day with no date and no zone — opening and closing times. Stored as
/// minutes since midnight rather than a time column: SQL Server's `time`, PostgreSQL's `time`
/// and SQLite's text-plus-convention are three different behaviours, and an int is the same
/// everywhere and orders correctly on all three.</summary>
public sealed class LocalTimeConverter : ValueConverter<LocalTime, int>
{
    public LocalTimeConverter()
        : base(
            time => (time.Hour * 60) + time.Minute,
            minutes => new LocalTime(minutes / 60, minutes % 60))
    {
    }
}
