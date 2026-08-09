using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>When a tenant is open, one row per weekday.
///
/// Tenant-level rather than per-module, because a business has one set of opening hours however
/// many modules it holds — the same reasoning that leaves Administration as the only shared
/// screen. Appointments will not offer a slot outside them and Orders will not promise a
/// delivery outside them.
///
/// A row per day rather than one open/close pair, because closed days are the point: a barber
/// shut on Monday and a shop working a short Saturday cannot be expressed by a single window,
/// and the hardcoded 09:00–21:00 this replaces offered callers appointments on days the business
/// was closed.</summary>
public sealed class BusinessHours : BaseEntity
{
    public int TenantId { get; private set; }
    public IsoDayOfWeek DayOfWeek { get; private set; }

    /// <summary>Both null when the business is closed that day. Kept as a pair rather than an
    /// IsClosed flag so there is one fact, not two that can disagree.</summary>
    public LocalTime? OpensAt { get; private set; }
    public LocalTime? ClosesAt { get; private set; }

    public bool IsClosed => OpensAt is null || ClosesAt is null;

    private BusinessHours()
    {
    }

    public static BusinessHours Open(int tenantId, IsoDayOfWeek dayOfWeek, LocalTime opensAt, LocalTime closesAt, Instant now)
    {
        if (closesAt <= opensAt)
        {
            throw new ArgumentException("A business must close after it opens.", nameof(closesAt));
        }

        var hours = new BusinessHours { TenantId = tenantId, DayOfWeek = dayOfWeek, OpensAt = opensAt, ClosesAt = closesAt };
        hours.InitBase(now);
        return hours;
    }

    public static BusinessHours Closed(int tenantId, IsoDayOfWeek dayOfWeek, Instant now)
    {
        var hours = new BusinessHours { TenantId = tenantId, DayOfWeek = dayOfWeek };
        hours.InitBase(now);
        return hours;
    }

    public void SetOpen(LocalTime opensAt, LocalTime closesAt, Instant now)
    {
        if (closesAt <= opensAt)
        {
            throw new ArgumentException("A business must close after it opens.", nameof(closesAt));
        }

        OpensAt = opensAt;
        ClosesAt = closesAt;
        Touch(now);
    }

    public void SetClosed(Instant now)
    {
        OpensAt = null;
        ClosesAt = null;
        Touch(now);
    }

    /// <summary>Whether something starting and ending at these local times fits inside the day.
    /// An appointment must finish before closing, not merely start before it.</summary>
    public bool Contains(LocalTime start, LocalTime end)
        => !IsClosed && start >= OpensAt!.Value && end <= ClosesAt!.Value;
}
