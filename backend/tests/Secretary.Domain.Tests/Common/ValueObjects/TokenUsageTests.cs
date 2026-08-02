using Secretary.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.ValueObjects;

/// <summary>The cached-token split is the one piece of this that is easy to get wrong and
/// invisible when you do: the bill still looks plausible, it is just several times too big.</summary>
public sealed class TokenUsageTests
{
    [Fact]
    public void Cached_tokens_are_carved_out_of_the_input_totals_they_arrive_inside()
    {
        // The provider says "10,000 audio input tokens, of which 8,000 were cached". Only the
        // remaining 2,000 are charged at the full audio rate.
        var usage = TokenUsage.FromProviderTotals(
            inputTextTokens: 1_000, cachedInputTextTokens: 600,
            inputAudioTokens: 10_000, cachedInputAudioTokens: 8_000,
            outputTextTokens: 50, outputAudioTokens: 400);

        usage.InputTextTokens.ShouldBe(400);
        usage.CachedInputTextTokens.ShouldBe(600);
        usage.InputAudioTokens.ShouldBe(2_000);
        usage.CachedInputAudioTokens.ShouldBe(8_000);
    }

    [Fact]
    public void Total_tokens_still_counts_every_token_the_call_consumed()
    {
        var usage = TokenUsage.FromProviderTotals(1_000, 600, 10_000, 8_000, 50, 400);

        // Splitting the input totals must not lose any of them along the way.
        usage.TotalTokens.ShouldBe(11_450);
    }

    [Fact]
    public void A_cached_count_larger_than_its_total_cannot_produce_negative_billable_tokens()
    {
        var usage = TokenUsage.FromProviderTotals(
            inputTextTokens: 100, cachedInputTextTokens: 500,
            inputAudioTokens: 0, cachedInputAudioTokens: 0,
            outputTextTokens: 0, outputAudioTokens: 0);

        usage.InputTextTokens.ShouldBe(0);
        usage.CachedInputTextTokens.ShouldBe(500);
    }

    [Fact]
    public void Usage_accumulates_across_the_turns_of_a_call()
    {
        var first = new TokenUsage(10, 20, 30, 40, 50, 60);
        var second = new TokenUsage(1, 2, 3, 4, 5, 6);

        (first + second).ShouldBe(new TokenUsage(11, 22, 33, 44, 55, 66));
    }

    [Fact]
    public void Zero_is_the_identity_for_a_call_that_consumed_nothing()
    {
        var usage = new TokenUsage(10, 20, 30, 40, 50, 60);

        (usage + TokenUsage.Zero).ShouldBe(usage);
        TokenUsage.Zero.TotalTokens.ShouldBe(0);
    }
}
