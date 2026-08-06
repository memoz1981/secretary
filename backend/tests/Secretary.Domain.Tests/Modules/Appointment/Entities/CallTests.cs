using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class CallTests
{
    private const int TenantId = 1;
    private static readonly Instant StartedAt = Instant.FromUtc(2026, 7, 11, 9, 0);
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 5);
    private static readonly TokenUsage NoUsage = TokenUsage.Zero;

    [Fact]
    public void Log_sets_expected_fields()
    {
        const int clientId = 10;
        var usage = new TokenUsage(
            InputTextTokens: 1_200, CachedInputTextTokens: 800,
            InputAudioTokens: 4_000, CachedInputAudioTokens: 2_500,
            OutputTextTokens: 60, OutputAudioTokens: 900);

        var call = Call.Log(
            TenantId, clientId, "+994000000", null, CallClassification.NewAppointment, CallOutcome.ResolvedByAgent,
            130, 6, 5, null, "https://recordings/1", "transcript", StartedAt, "gpt-realtime-2.1", CallPipeline.OpenAiRealtime_2_1, usage, 0.3142m, Now);

        call.TenantId.ShouldBe(TenantId);
        call.ClientId.ShouldBe(clientId);
        call.CallerPhoneNumber.ShouldBe("+994000000");
        call.Classification.ShouldBe(CallClassification.NewAppointment);
        call.Outcome.ShouldBe(CallOutcome.ResolvedByAgent);
        call.DurationSeconds.ShouldBe(130);
        call.TurnCount.ShouldBe(6);
        call.CallerTurnCount.ShouldBe(5);
        call.RecordingUrl.ShouldBe("https://recordings/1");
        call.Transcript.ShouldBe("transcript");
        call.StartedAt.ShouldBe(StartedAt);
        call.Status.ShouldBe(EntityStatus.Active);
        call.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Log_stores_the_price_it_was_given_rather_than_deriving_one()
    {
        // The whole point of the snapshot: the entity holds a number someone else worked out,
        // so a later change to the rate card can't reach back and alter this call.
        var usage = new TokenUsage(1_000, 0, 2_000, 0, 100, 500);

        var call = Call.Log(
            TenantId, null, "+994000000", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent,
            60, 3, 3, null, "url", null, StartedAt, "gpt-realtime-2.1", CallPipeline.OpenAiRealtime_2_1, usage, 0.12345678m, Now);

        call.CostUsd.ShouldBe(0.12345678m);
        call.AgentModel.ShouldBe("gpt-realtime-2.1");
        call.TokenUsage.ShouldBe(usage);
        call.TokenUsage.TotalTokens.ShouldBe(3_600);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Log_throws_when_caller_phone_number_is_blank(string? phoneNumber)
    {
        Should.Throw<ArgumentException>(() => Call.Log(
            TenantId, null, phoneNumber!, null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent,
            10, 1, 1, null, "url", null, StartedAt, "", CallPipeline.Unknown, NoUsage, 0m, Now));
    }

    [Fact]
    public void Log_throws_when_duration_is_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Call.Log(
            TenantId, null, "+994000000", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent,
            -1, 1, 1, null, "url", null, StartedAt, "", CallPipeline.Unknown, NoUsage, 0m, Now));
    }

    [Fact]
    public void Log_throws_when_turn_count_is_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Call.Log(
            TenantId, null, "+994000000", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent,
            10, -1, 1, null, "url", null, StartedAt, "", CallPipeline.Unknown, NoUsage, 0m, Now));
    }

    [Fact]
    public void Log_throws_when_caller_turn_count_is_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Call.Log(
            TenantId, null, "+994000000", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent,
            10, 1, -1, null, "url", null, StartedAt, "", CallPipeline.Unknown, NoUsage, 0m, Now));
    }

    [Fact]
    public void Log_throws_when_cost_is_negative()
    {
        // A negative call would subtract from the month's spend — worse than being missing,
        // because nothing downstream would flag it.
        Should.Throw<ArgumentOutOfRangeException>(() => Call.Log(
            TenantId, null, "+994000000", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent,
            10, 1, 1, null, "url", null, StartedAt, "gpt-realtime-2.1", CallPipeline.OpenAiRealtime_2_1, NoUsage, -0.01m, Now));
    }
}
