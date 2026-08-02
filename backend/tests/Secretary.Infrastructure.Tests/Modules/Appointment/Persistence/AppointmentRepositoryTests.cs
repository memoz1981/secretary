using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Infrastructure.Persistence;
using Secretary.Infrastructure.Persistence.Repositories;
using Secretary.Infrastructure.Tests.TestSupport;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Infrastructure.Tests.Persistence;

public sealed class AppointmentRepositoryTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 8, 0);

    /// <summary>Seeds tenant + client + provider + offering (real FKs require the parents)
    /// and returns their identity ids.</summary>
    private static (int TenantId, int ClientId, int ProviderId, int OfferingId) SeedGraph(AppDbContext context)
    {
        var tenant = TestSeed.AddTenant(context);
        var client = TestSeed.AddClient(context, tenant.Id);
        var provider = TestSeed.AddProvider(context, tenant.Id);
        var offering = TestSeed.AddOffering(context, tenant.Id);
        return (tenant.Id, client.Id, provider.Id, offering.Id);
    }

    [Fact]
    public async Task GetNeedingReminderAcrossAllTenantsAsync_returns_confirmed_appointments_from_every_tenant()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graphA;
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graphB;
        using (var seed = factory.CreateContext(null))
        {
            graphA = SeedGraph(seed);
            graphB = SeedGraph(seed);
        }

        using var context = factory.CreateContext(graphA.TenantId); // ambient tenant is irrelevant — IgnoreQueryFilters bypasses it
        var repository = new AppointmentRepository(context);

        var thisTenantAppt = Appointment.Create(
            graphA.TenantId, graphA.ClientId, graphA.ProviderId, graphA.OfferingId,
            Instant.FromUtc(2026, 7, 11, 5, 0), Instant.FromUtc(2026, 7, 11, 5, 30), null, AppointmentCreatedBy.Staff, Now);
        var otherTenantAppt = Appointment.Create(
            graphB.TenantId, graphB.ClientId, graphB.ProviderId, graphB.OfferingId,
            Instant.FromUtc(2026, 7, 11, 6, 0), Instant.FromUtc(2026, 7, 11, 6, 30), null, AppointmentCreatedBy.Staff, Now);
        var cancelledAppt = Appointment.Create(
            graphA.TenantId, graphA.ClientId, graphA.ProviderId, graphA.OfferingId,
            Instant.FromUtc(2026, 7, 11, 7, 0), Instant.FromUtc(2026, 7, 11, 7, 30), null, AppointmentCreatedBy.Staff, Now);
        cancelledAppt.Cancel(Now);

        context.Appointments.AddRange(thisTenantAppt, otherTenantAppt, cancelledAppt);
        await context.SaveChangesAsync();

        var results = await repository.GetNeedingReminderAcrossAllTenantsAsync(
            Instant.FromUtc(2026, 7, 11, 0, 0), Instant.FromUtc(2026, 7, 12, 0, 0), default);

        results.Count.ShouldBe(2);
        results.ShouldContain(a => a.Id == thisTenantAppt.Id);
        results.ShouldContain(a => a.Id == otherTenantAppt.Id);
        results.ShouldNotContain(a => a.Id == cancelledAppt.Id);
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_finds_the_matching_appointment()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
        }

        using var context = factory.CreateContext(graph.TenantId);
        var repository = new AppointmentRepository(context);

        var appointment = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 9, 0), Instant.FromUtc(2026, 7, 11, 9, 30), null, AppointmentCreatedBy.Agent, Now, "retry-key-abc");
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var found = await repository.GetByIdempotencyKeyAsync("retry-key-abc", default);

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(appointment.Id);
    }

    [Fact]
    public async Task Saving_a_second_appointment_with_the_same_idempotency_key_violates_the_unique_index()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
        }

        using var context = factory.CreateContext(graph.TenantId);

        context.Appointments.Add(Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 9, 0), Instant.FromUtc(2026, 7, 11, 9, 30), null, AppointmentCreatedBy.Agent, Now, "duplicate-key"));
        await context.SaveChangesAsync();

        context.Appointments.Add(Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 11, 0), Instant.FromUtc(2026, 7, 11, 11, 30), null, AppointmentCreatedBy.Agent, Now, "duplicate-key"));

        await Should.ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Saving_a_second_appointment_at_the_same_provider_and_start_violates_the_unique_index()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
        }

        using var context = factory.CreateContext(graph.TenantId);
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = Instant.FromUtc(2026, 7, 11, 9, 30);

        context.Appointments.Add(Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId, start, end, null, AppointmentCreatedBy.Staff, Now));
        await context.SaveChangesAsync();

        context.Appointments.Add(Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId, start, end, null, AppointmentCreatedBy.Staff, Now));

        await Should.ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_cancelled_appointment_does_not_block_a_new_one_at_the_same_provider_and_start()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
        }

        using var context = factory.CreateContext(graph.TenantId);
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = Instant.FromUtc(2026, 7, 11, 9, 30);

        var cancelled = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId, start, end, null, AppointmentCreatedBy.Staff, Now);
        context.Appointments.Add(cancelled);
        await context.SaveChangesAsync();
        cancelled.Cancel(Now);
        await context.SaveChangesAsync();

        context.Appointments.Add(Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId, start, end, null, AppointmentCreatedBy.Staff, Now));

        await Should.NotThrowAsync(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task FindOverlappingAsync_returns_only_appointments_that_actually_overlap()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
        }

        using var context = factory.CreateContext(graph.TenantId);
        var repository = new AppointmentRepository(context);

        var nineToNineThirty = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 9, 0), Instant.FromUtc(2026, 7, 11, 9, 30), null, AppointmentCreatedBy.Staff, Now);
        var tenToTenThirty = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 10, 0), Instant.FromUtc(2026, 7, 11, 10, 30), null, AppointmentCreatedBy.Staff, Now);

        context.Appointments.AddRange(nineToNineThirty, tenToTenThirty);
        await context.SaveChangesAsync();

        var overlapping = await repository.FindOverlappingAsync(
            graph.ProviderId, Instant.FromUtc(2026, 7, 11, 9, 15), Instant.FromUtc(2026, 7, 11, 9, 45), default);

        overlapping.Count.ShouldBe(1);
        overlapping[0].Id.ShouldBe(nineToNineThirty.Id);
    }

    [Fact]
    public async Task FindOverlappingAsync_only_matches_the_requested_provider()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        int otherProviderId;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
            otherProviderId = TestSeed.AddProvider(seed, graph.TenantId, "Kamran").Id;
        }

        using var context = factory.CreateContext(graph.TenantId);
        var repository = new AppointmentRepository(context);

        var appointment = Appointment.Create(
            graph.TenantId, graph.ClientId, otherProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 9, 0), Instant.FromUtc(2026, 7, 11, 9, 30), null, AppointmentCreatedBy.Staff, Now);

        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var overlapping = await repository.FindOverlappingAsync(
            graph.ProviderId, Instant.FromUtc(2026, 7, 11, 9, 0), Instant.FromUtc(2026, 7, 11, 9, 30), default);

        overlapping.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetForDateRangeAsync_filters_by_provider_and_orders_by_start()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        int otherProviderId;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
            otherProviderId = TestSeed.AddProvider(seed, graph.TenantId, "Kamran").Id;
        }

        using var context = factory.CreateContext(graph.TenantId);
        var repository = new AppointmentRepository(context);

        var later = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 11, 0), Instant.FromUtc(2026, 7, 11, 11, 30), null, AppointmentCreatedBy.Staff, Now);
        var earlier = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 9, 0), Instant.FromUtc(2026, 7, 11, 9, 30), null, AppointmentCreatedBy.Staff, Now);
        var otherProviderAppointment = Appointment.Create(
            graph.TenantId, graph.ClientId, otherProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 10, 0), Instant.FromUtc(2026, 7, 11, 10, 30), null, AppointmentCreatedBy.Staff, Now);

        context.Appointments.AddRange(later, earlier, otherProviderAppointment);
        await context.SaveChangesAsync();

        var results = await repository.GetForDateRangeAsync(
            Instant.FromUtc(2026, 7, 11, 0, 0), Instant.FromUtc(2026, 7, 12, 0, 0), graph.ProviderId, default);

        results.Count.ShouldBe(2);
        results[0].Id.ShouldBe(earlier.Id);
        results[1].Id.ShouldBe(later.Id);
    }

    [Fact]
    public async Task GetNeedingReminderAsync_only_returns_confirmed_appointments_in_range()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
        }

        using var context = factory.CreateContext(graph.TenantId);
        var repository = new AppointmentRepository(context);

        var confirmed = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 9, 0), Instant.FromUtc(2026, 7, 11, 9, 30), null, AppointmentCreatedBy.Staff, Now);
        var cancelled = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId,
            Instant.FromUtc(2026, 7, 11, 10, 0), Instant.FromUtc(2026, 7, 11, 10, 30), null, AppointmentCreatedBy.Staff, Now);
        cancelled.Cancel(Now);

        context.Appointments.AddRange(confirmed, cancelled);
        await context.SaveChangesAsync();

        var results = await repository.GetNeedingReminderAsync(
            Instant.FromUtc(2026, 7, 11, 0, 0), Instant.FromUtc(2026, 7, 12, 0, 0), default);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(confirmed.Id);
    }
}
