using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using Secretary.Infrastructure.Persistence;
using Secretary.Infrastructure.Persistence.Repositories;
using Secretary.Infrastructure.Tests.TestSupport;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Infrastructure.Tests.Persistence;

public sealed class CallRepositoryTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 8, 0);

    /// <summary>These tests are about filtering and ordering, so cost is incidental — but it
    /// still has to round-trip, which is why one call below carries a real usage/price.</summary>
    private static Call LogCall(
        int tenantId, string phoneNumber, int? appointmentId, CallClassification classification,
        CallOutcome outcome, Instant startedAt, TokenUsage? usage = null, decimal costUsd = 0m)
        => Call.Log(
            tenantId, null, phoneNumber, appointmentId, classification, outcome, 60, 3, 3, null, "url", null,
            startedAt, "gpt-realtime-2.1", CallPipeline.OpenAiRealtime_2_1, usage ?? TokenUsage.Zero, costUsd, Now);

    private static (int TenantId, int ClientId, int ProviderId, int OfferingId) SeedGraph(AppDbContext context)
    {
        var tenant = TestSeed.AddTenant(context);
        var client = TestSeed.AddClient(context, tenant.Id);
        var provider = TestSeed.AddProvider(context, tenant.Id);
        var offering = TestSeed.AddOffering(context, tenant.Id);
        return (tenant.Id, client.Id, provider.Id, offering.Id);
    }

    [Fact]
    public async Task SearchAsync_filters_by_provider_through_the_related_appointment()
    {
        using var factory = new SqliteDbContextFactory();
        (int TenantId, int ClientId, int ProviderId, int OfferingId) graph;
        int providerB;
        using (var seed = factory.CreateContext(null))
        {
            graph = SeedGraph(seed);
            providerB = TestSeed.AddProvider(seed, graph.TenantId, "Kamran").Id;
        }

        using var context = factory.CreateContext(graph.TenantId);

        var appointmentForA = Appointment.Create(
            graph.TenantId, graph.ClientId, graph.ProviderId, graph.OfferingId, Now, Now + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, Now);
        var appointmentForB = Appointment.Create(
            graph.TenantId, graph.ClientId, providerB, graph.OfferingId, Now, Now + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, Now);
        context.Appointments.AddRange(appointmentForA, appointmentForB);
        await context.SaveChangesAsync();

        var callForA = LogCall(graph.TenantId, "+994111", appointmentForA.Id, CallClassification.NewAppointment, CallOutcome.ResolvedByAgent, Now);
        var callForB = LogCall(graph.TenantId, "+994222", appointmentForB.Id, CallClassification.NewAppointment, CallOutcome.ResolvedByAgent, Now);
        var callWithNoAppointment = LogCall(graph.TenantId, "+994333", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent, Now);
        context.Calls.AddRange(callForA, callForB, callWithNoAppointment);
        await context.SaveChangesAsync();

        var repository = new CallRepository(context);
        var results = await repository.SearchAsync(null, null, null, null, graph.ProviderId, null, default);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(callForA.Id);
    }

    [Fact]
    public async Task SearchAsync_filters_by_classification_and_outcome()
    {
        using var factory = new SqliteDbContextFactory();
        int tenantId;
        using (var seed = factory.CreateContext(null))
        {
            tenantId = TestSeed.AddTenant(seed).Id;
        }

        using var context = factory.CreateContext(tenantId);

        var match = LogCall(tenantId, "+994111", null, CallClassification.NewAppointment, CallOutcome.ResolvedByAgent, Now);
        var wrongClassification = LogCall(tenantId, "+994222", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent, Now);
        var wrongOutcome = LogCall(tenantId, "+994333", null, CallClassification.NewAppointment, CallOutcome.FailedNoAvailability, Now);
        context.Calls.AddRange(match, wrongClassification, wrongOutcome);
        await context.SaveChangesAsync();

        var repository = new CallRepository(context);
        var results = await repository.SearchAsync(null, null, CallClassification.NewAppointment, CallOutcome.ResolvedByAgent, null, null, default);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(match.Id);
    }

    [Fact]
    public async Task SearchAsync_orders_by_started_at_descending()
    {
        using var factory = new SqliteDbContextFactory();
        int tenantId;
        using (var seed = factory.CreateContext(null))
        {
            tenantId = TestSeed.AddTenant(seed).Id;
        }

        using var context = factory.CreateContext(tenantId);

        var earlier = LogCall(tenantId, "+994111", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent, Now);
        var later = LogCall(tenantId, "+994222", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent, Now + Duration.FromHours(1));
        context.Calls.AddRange(earlier, later);
        await context.SaveChangesAsync();

        var repository = new CallRepository(context);
        var results = await repository.SearchAsync(null, null, null, null, null, null, default);

        results[0].Id.ShouldBe(later.Id);
        results[1].Id.ShouldBe(earlier.Id);
    }

    [Fact]
    public async Task Token_usage_and_cost_survive_a_round_trip_to_the_database()
    {
        using var factory = new SqliteDbContextFactory();
        int tenantId;
        using (var seed = factory.CreateContext(null))
        {
            tenantId = TestSeed.AddTenant(seed).Id;
        }

        var usage = new TokenUsage(
            InputTextTokens: 12_345, CachedInputTextTokens: 6_789,
            InputAudioTokens: 98_765, CachedInputAudioTokens: 43_210,
            OutputTextTokens: 321, OutputAudioTokens: 27_654);

        int callId;
        using (var context = factory.CreateContext(tenantId))
        {
            // Eight decimal places: the stored price has to come back exactly, not rounded to
            // the cent, or a month of calls drifts away from the provider's invoice.
            var call = LogCall(
                tenantId, "+994111", null, CallClassification.NewAppointment, CallOutcome.ResolvedByAgent,
                Now, usage, 3.45678901m);
            context.Calls.Add(call);
            await context.SaveChangesAsync();
            callId = call.Id;
        }

        using var reader = factory.CreateContext(tenantId);
        var repository = new CallRepository(reader);
        var stored = await repository.GetByIdAsync(callId, default);

        stored.ShouldNotBeNull();
        stored!.AgentModel.ShouldBe("gpt-realtime-2.1");
        stored.CostUsd.ShouldBe(3.45678901m);
        stored.TokenUsage.ShouldBe(usage);
    }
}
