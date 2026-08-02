using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class AppointmentTests
{
    private const int TenantId = 1;
    private const int ClientId = 10;
    private const int ProviderId = 20;
    private const int ServiceOfferingId = 30;
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);
    private static readonly Instant Start = Instant.FromUtc(2026, 7, 11, 10, 0);
    private static readonly Instant End = Instant.FromUtc(2026, 7, 11, 10, 30);

    private static Appointment CreateAppointment()
        => Appointment.Create(TenantId, ClientId, ProviderId, ServiceOfferingId, Start, End, "Notes", AppointmentCreatedBy.Staff, Now);

    [Fact]
    public void Create_starts_confirmed_and_not_flagged()
    {
        var appointment = CreateAppointment();

        appointment.AppointmentStatus.ShouldBe(AppointmentStatus.Confirmed);
        appointment.Status.ShouldBe(EntityStatus.Active);
        appointment.ReminderNoAnswerFlag.ShouldBeFalse();
        appointment.CreatedAt.ShouldBe(Now);
        appointment.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_throws_when_end_is_not_after_start()
    {
        Should.Throw<ArgumentException>(() =>
            Appointment.Create(TenantId, ClientId, ProviderId, ServiceOfferingId, Start, Start, null, AppointmentCreatedBy.Staff, Now));
    }

    [Fact]
    public void Reschedule_updates_provider_service_and_times()
    {
        var appointment = CreateAppointment();
        const int newProvider = 21;
        const int newService = 31;
        var newStart = Start + Duration.FromHours(1);
        var newEnd = End + Duration.FromHours(1);
        var later = Now + Duration.FromMinutes(1);

        appointment.Reschedule(newProvider, newService, newStart, newEnd, later);

        appointment.ProviderId.ShouldBe(newProvider);
        appointment.ServiceOfferingId.ShouldBe(newService);
        appointment.Start.ShouldBe(newStart);
        appointment.End.ShouldBe(newEnd);
        appointment.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void Reschedule_throws_when_appointment_already_cancelled()
    {
        var appointment = CreateAppointment();
        appointment.Cancel(Now);

        Should.Throw<InvalidStateTransitionException>(() => appointment.Reschedule(ProviderId, ServiceOfferingId, Start, End, Now));
    }

    [Fact]
    public void Cancel_sets_status_cancelled()
    {
        var appointment = CreateAppointment();

        appointment.Cancel(Now);

        appointment.AppointmentStatus.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_twice_throws()
    {
        var appointment = CreateAppointment();
        appointment.Cancel(Now);

        Should.Throw<InvalidStateTransitionException>(() => appointment.Cancel(Now));
    }

    [Fact]
    public void MarkReminderNoAnswer_sets_flag()
    {
        var appointment = CreateAppointment();

        appointment.MarkReminderNoAnswer(Now);

        appointment.ReminderNoAnswerFlag.ShouldBeTrue();
    }

    [Fact]
    public void MarkReminderNoAnswer_throws_when_cancelled()
    {
        var appointment = CreateAppointment();
        appointment.Cancel(Now);

        Should.Throw<InvalidStateTransitionException>(() => appointment.MarkReminderNoAnswer(Now));
    }

    [Fact]
    public void ConfirmReminder_clears_flag_and_confirms()
    {
        var appointment = CreateAppointment();
        appointment.MarkReminderNoAnswer(Now);

        appointment.ConfirmReminder(Now);

        appointment.ReminderNoAnswerFlag.ShouldBeFalse();
        appointment.AppointmentStatus.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public void UpdateNotes_changes_notes_and_updated_at()
    {
        var appointment = CreateAppointment();
        var later = Now + Duration.FromMinutes(1);

        appointment.UpdateNotes("New notes", later);

        appointment.Notes.ShouldBe("New notes");
        appointment.UpdatedAt.ShouldBe(later);
    }
}
