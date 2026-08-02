using Secretary.Api.Scheduling;
using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Shouldly;
using Xunit;

namespace Secretary.Api.Tests.Scheduling;

public sealed class ReminderSchedulerJobTests
{
    [Fact]
    public async Task RunAsync_notifies_once_per_appointment_needing_a_reminder()
    {
        // "Now" is 06:00 UTC, which is 10:00 in Asia/Baku (UTC+4) — safely mid-morning,
        // so there's no ambiguity about which Baku calendar day "today" is.
        var now = Instant.FromUtc(2026, 7, 11, 6, 0);
        var clock = new FakeClock(now);

        var bakuZone = DateTimeZoneProviders.Tzdb["Asia/Baku"];
        var todayInBaku = now.InZone(bakuZone).Date;
        var expectedStart = todayInBaku.AtStartOfDayInZone(bakuZone).ToInstant();
        var expectedEnd = todayInBaku.PlusDays(1).AtStartOfDayInZone(bakuZone).ToInstant();

        var appointment = Appointment.Create(
            1, 10, 20, 30,
            expectedStart + Duration.FromHours(3), expectedStart + Duration.FromHours(3) + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, now);

        var uow = new Mock<IUnitOfWork>();
        var appointments = new Mock<IAppointmentRepository>();
        uow.SetupGet(u => u.Appointments).Returns(appointments.Object);
        appointments
            .Setup(a => a.GetNeedingReminderAcrossAllTenantsAsync(expectedStart, expectedEnd, default))
            .ReturnsAsync([appointment]);

        var notifier = new Mock<IOutboundReminderNotifier>();

        var sut = new ReminderSchedulerJob(uow.Object, notifier.Object, clock, NullLogger<ReminderSchedulerJob>.Instance);

        await sut.RunAsync(default);

        notifier.Verify(n => n.NotifyReminderDueAsync(appointment, default), Times.Once);
    }

    [Fact]
    public async Task RunAsync_does_nothing_when_no_appointments_need_a_reminder()
    {
        var now = Instant.FromUtc(2026, 7, 11, 6, 0);
        var clock = new FakeClock(now);

        var uow = new Mock<IUnitOfWork>();
        var appointments = new Mock<IAppointmentRepository>();
        uow.SetupGet(u => u.Appointments).Returns(appointments.Object);
        appointments
            .Setup(a => a.GetNeedingReminderAcrossAllTenantsAsync(It.IsAny<Instant>(), It.IsAny<Instant>(), default))
            .ReturnsAsync([]);

        var notifier = new Mock<IOutboundReminderNotifier>();
        var sut = new ReminderSchedulerJob(uow.Object, notifier.Object, clock, NullLogger<ReminderSchedulerJob>.Instance);

        await sut.RunAsync(default);

        notifier.Verify(n => n.NotifyReminderDueAsync(It.IsAny<Appointment>(), default), Times.Never);
    }
}
