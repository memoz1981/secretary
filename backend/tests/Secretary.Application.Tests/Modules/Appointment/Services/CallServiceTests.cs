using Secretary.Application.Dtos;
using Secretary.Application.Pricing;
using Secretary.Application.Services;
using Secretary.Application.Tests.TestSupport;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using Secretary.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests.Services;

public sealed class CallServiceTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);
    private const int TenantId = 7;
    private const string Model = "gpt-realtime-2.1";

    private readonly FakeUnitOfWork _uow = new();
    private readonly CallService _sut;

    public CallServiceTests()
    {
        _sut = new CallService(_uow.Object, new FakeClock(Now), new FakeCurrentTenantProvider(TenantId), Pricebook());
    }

    // The three models a chained V2 call bills, at their published rates.
    private const string TranscribeModel = "gpt-4o-transcribe";
    private const string SpeechModel = "gpt-4o-mini-tts";
    private const string ChatModel = "gpt-5.6";

    /// <summary>OpenAI's published rates, so the arithmetic in these tests is checkable against
    /// the real cards rather than against invented round numbers.</summary>
    internal static TokenPricebook Pricebook() => new(Options.Create(new ModelPricingOptions
    {
        Models = new Dictionary<string, ModelTokenRates>(StringComparer.OrdinalIgnoreCase)
        {
            [Model] = new()
            {
                TextInputPerMillionUsd = 4.00m,
                CachedTextInputPerMillionUsd = 0.40m,
                TextOutputPerMillionUsd = 24.00m,
                AudioInputPerMillionUsd = 32.00m,
                CachedAudioInputPerMillionUsd = 0.40m,
                AudioOutputPerMillionUsd = 64.00m,
            },
            [TranscribeModel] = new()
            {
                TextInputPerMillionUsd = 2.50m,
                AudioInputPerMillionUsd = 2.50m,
                TextOutputPerMillionUsd = 10.00m,
            },
            [SpeechModel] = new()
            {
                TextInputPerMillionUsd = 0.60m,
                AudioOutputPerMillionUsd = 12.00m,
            },
            [ChatModel] = new()
            {
                TextInputPerMillionUsd = 5.00m,
                CachedTextInputPerMillionUsd = 0.50m,
                TextOutputPerMillionUsd = 30.00m,
            },
        },
    }));

    private static LogCallRequest Request(
        int durationSeconds = 120, int turnCount = 5, int callerTurnCount = 4,
        string agentModel = Model, TokenUsage? usage = null,
        CallClassification classification = CallClassification.NewAppointment)
        => Request(
            usage is null ? [] : [new ModelUsage(agentModel, usage)],
            durationSeconds, turnCount, callerTurnCount, classification);

    private static LogCallRequest Request(
        IReadOnlyList<ModelUsage> usages,
        int durationSeconds = 120, int turnCount = 5, int callerTurnCount = 4,
        CallClassification classification = CallClassification.NewAppointment)
        => new(
            "+994000000", null, classification, CallOutcome.ResolvedByAgent,
            durationSeconds, turnCount, callerTurnCount, null, "url", null, Now, CallPipeline.OpenAiRealtime_2_1, usages);

    private static Call LoggedCall(int durationSeconds = 30, int turnCount = 2, string? transcript = null)
        => Call.Log(
            TenantId, null, "+994000000", null, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent,
            durationSeconds, turnCount, turnCount, null, "url", transcript, Now, Model, CallPipeline.OpenAiRealtime_2_1, TokenUsage.Zero, 0m, Now);

    [Fact]
    public async Task LogAsync_resolves_client_by_caller_phone_number()
    {
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq Aliyev");
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync(client);

        var result = await _sut.LogAsync(Request(), default);

        result.ClientName.ShouldBe("Tofiq Aliyev");
        _uow.Calls.Verify(c => c.AddAsync(It.IsAny<Call>(), default), Times.Once);
    }

    [Fact]
    public async Task LogAsync_allows_unresolved_client()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.LogAsync(
            Request(durationSeconds: 30, turnCount: 2, callerTurnCount: 2, classification: CallClassification.InquiryOther),
            default);

        result.ClientId.ShouldBeNull();
        result.ClientName.ShouldBeNull();
    }

    [Fact]
    public async Task LogAsync_requires_a_tenant_scoped_caller()
    {
        var sut = new CallService(_uow.Object, new FakeClock(Now), new FakeCurrentTenantProvider(null), Pricebook());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.LogAsync(Request(), default));
    }

    [Fact]
    public async Task LogAsync_prices_the_call_from_its_token_usage()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        // 100k audio in at $32/M = $3.20; 50k cached audio in at $0.40/M = $0.02;
        // 20k text in at $4/M = $0.08; 10k cached text in at $0.40/M = $0.004;
        // 1k text out at $24/M = $0.024; 30k audio out at $64/M = $1.92. Total $5.248.
        var usage = new TokenUsage(
            InputTextTokens: 20_000, CachedInputTextTokens: 10_000,
            InputAudioTokens: 100_000, CachedInputAudioTokens: 50_000,
            OutputTextTokens: 1_000, OutputAudioTokens: 30_000);

        var result = await _sut.LogAsync(Request(usage: usage), default);

        result.CostUsd.ShouldBe(5.248m);
        result.TokenUsage.TotalTokens.ShouldBe(211_000);
    }

    [Fact]
    public async Task LogAsync_prices_a_call_on_an_unknown_model_at_zero_rather_than_failing()
    {
        // Losing the whole call record to save a cost figure would be the worse trade — the
        // guard against an unpriced model is at startup, not here.
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.LogAsync(
            Request(agentModel: "some-model-nobody-configured", usage: new TokenUsage(1, 1, 1, 1, 1, 1)),
            default);

        result.CostUsd.ShouldBe(0m);
        _uow.Calls.Verify(c => c.AddAsync(It.IsAny<Call>(), default), Times.Once);
    }

    [Fact]
    public async Task A_chained_call_is_priced_per_model_not_by_one_blended_rate()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        // One V2 call: recognition heard 60k audio tokens, the model read 40k text (30k of it
        // cached) and wrote 2k, and synthesis spoke 20k audio tokens from 3k of text.
        //   transcribe  60,000 audio @ $2.50/M  = $0.150
        //   gpt-5.6     10,000 text  @ $5.00/M  = $0.050
        //               30,000 cached @ $0.50/M = $0.015
        //                2,000 out   @ $30.00/M = $0.060
        //   mini-tts     3,000 text  @ $0.60/M  = $0.0018
        //               20,000 audio @ $12.00/M = $0.240
        // Total $0.5168. Summing the tokens first and pricing once could not produce this.
        var result = await _sut.LogAsync(
            Request([
                new ModelUsage(TranscribeModel, new TokenUsage(0, 0, 60_000, 0, 0, 0)),
                new ModelUsage(ChatModel, new TokenUsage(10_000, 30_000, 0, 0, 2_000, 0)),
                new ModelUsage(SpeechModel, new TokenUsage(3_000, 0, 0, 0, 0, 20_000)),
            ]),
            default);

        result.CostUsd.ShouldBe(0.5168m);

        // Tokens are still aggregated, but only for display — they span three rate cards.
        result.TokenUsage.TotalTokens.ShouldBe(125_000);
    }

    [Fact]
    public async Task A_chained_call_records_every_model_that_served_it()
    {
        // Naming only one would make a jump in the cost charts impossible to explain later.
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.LogAsync(
            Request([
                new ModelUsage(TranscribeModel, new TokenUsage(0, 0, 100, 0, 0, 0)),
                new ModelUsage(ChatModel, new TokenUsage(100, 0, 0, 0, 10, 0)),
                new ModelUsage(SpeechModel, new TokenUsage(50, 0, 0, 0, 0, 200)),
            ]),
            default);

        result.AgentModel.ShouldBe("gpt-4o-transcribe + gpt-5.6 + gpt-4o-mini-tts");
    }

    [Fact]
    public async Task LogAsync_costs_nothing_for_a_call_that_consumed_no_tokens()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.LogAsync(Request(usage: null), default);

        result.CostUsd.ShouldBe(0m);
        result.TokenUsage.ShouldBe(TokenUsage.Zero);
    }

    [Fact]
    public async Task Per_minute_and_per_answer_rates_come_from_the_stored_cost()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        // $0.64 of audio output over a 2-minute call with 4 answers.
        var result = await _sut.LogAsync(
            Request(durationSeconds: 120, turnCount: 4, usage: new TokenUsage(0, 0, 0, 0, 0, 10_000)),
            default);

        result.CostUsd.ShouldBe(0.64m);
        result.CostPerMinuteUsd.ShouldBe(0.32m);
        result.CostPerAnswerUsd.ShouldBe(0.16m);
    }

    [Fact]
    public async Task A_call_with_no_measurable_duration_or_answers_reports_no_rate()
    {
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.LogAsync(Request(durationSeconds: 0, turnCount: 0, callerTurnCount: 0), default);

        result.CostPerMinuteUsd.ShouldBeNull();
        result.CostPerAnswerUsd.ShouldBeNull();
    }

    [Fact]
    public async Task GetDetailAsync_throws_not_found_when_missing()
    {
        _uow.Calls.Setup(c => c.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Call?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.GetDetailAsync(42, default));
    }

    [Fact]
    public async Task GetDetailAsync_returns_transcript_alongside_call()
    {
        var call = LoggedCall(transcript: "hello");
        _uow.Calls.Setup(c => c.GetByIdAsync(call.Id, default)).ReturnsAsync(call);

        var result = await _sut.GetDetailAsync(call.Id, default);

        result.Transcript.ShouldBe("hello");
        result.Call.Id.ShouldBe(call.Id);
    }

    [Fact]
    public async Task SearchAsync_delegates_to_repository_with_all_filters()
    {
        var call = LoggedCall();
        var request = new CallSearchRequest(Now, Now, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent, null);

        _uow.Calls
            .Setup(c => c.SearchAsync(Now, Now, CallClassification.InquiryOther, CallOutcome.ResolvedByAgent, null, null, default))
            .ReturnsAsync([call]);

        var result = await _sut.SearchAsync(request, default);

        result.Count.ShouldBe(1);
    }
}
