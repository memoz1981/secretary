#pragma warning disable OPENAI002 // OpenAI.Realtime is still an evolving/preview surface of the OpenAI SDK.
using System.ClientModel.Primitives;
using Secretary.Agents.Realtime;
using OpenAI.Realtime;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>What a call cost is only as trustworthy as this addition. The usage objects here are
/// built from the wire JSON OpenAI actually sends, rather than from hand-made stubs, so the
/// field-name assumptions the tally depends on are part of what's under test.</summary>
public sealed class RealtimeUsageTallyTests
{
    private static RealtimeResponseUsage Usage(string json)
        => ModelReaderWriter.Read<RealtimeResponseUsage>(BinaryData.FromString(json))!;

    /// <summary>One response's usage in OpenAI's shape: the input text/audio counts INCLUDE
    /// their cached portions, which is the detail that makes or breaks the price.</summary>
    private static RealtimeResponseUsage Response(
        int inputText, int inputAudio, int cachedText, int cachedAudio, int outputText, int outputAudio)
        => Usage($$"""
        {
          "total_tokens": {{inputText + inputAudio + outputText + outputAudio}},
          "input_tokens": {{inputText + inputAudio}},
          "output_tokens": {{outputText + outputAudio}},
          "input_token_details": {
            "text_tokens": {{inputText}},
            "audio_tokens": {{inputAudio}},
            "cached_tokens": {{cachedText + cachedAudio}},
            "cached_tokens_details": { "text_tokens": {{cachedText}}, "audio_tokens": {{cachedAudio}} }
          },
          "output_token_details": { "text_tokens": {{outputText}}, "audio_tokens": {{outputAudio}} }
        }
        """);

    [Fact]
    public void A_fresh_tally_has_consumed_nothing()
    {
        new RealtimeUsageTally().Total.TotalTokens.ShouldBe(0);
    }

    [Fact]
    public void One_response_is_split_into_billable_and_cached_tokens()
    {
        var tally = new RealtimeUsageTally();

        tally.Add(Response(inputText: 1_500, inputAudio: 2_500, cachedText: 400, cachedAudio: 500, outputText: 200, outputAudio: 800));

        var total = tally.Total;
        total.InputTextTokens.ShouldBe(1_100);
        total.CachedInputTextTokens.ShouldBe(400);
        total.InputAudioTokens.ShouldBe(2_000);
        total.CachedInputAudioTokens.ShouldBe(500);
        total.OutputTextTokens.ShouldBe(200);
        total.OutputAudioTokens.ShouldBe(800);
        total.TotalTokens.ShouldBe(5_000);
    }

    [Fact]
    public void A_call_costs_the_sum_of_its_responses_not_the_last_one()
    {
        // Each response is billed the whole conversation so far, so these figures grow through
        // the call — taking the final response's numbers as the call's total was the tempting
        // wrong answer, and it would understate a long call several times over.
        var tally = new RealtimeUsageTally();

        tally.Add(Response(500, 1_000, 0, 0, 50, 300));
        tally.Add(Response(900, 2_400, 400, 800, 60, 400));
        tally.Add(Response(1_400, 4_100, 800, 2_000, 40, 250));

        var total = tally.Total;
        total.CachedInputTextTokens.ShouldBe(1_200);
        total.CachedInputAudioTokens.ShouldBe(2_800);
        total.InputTextTokens.ShouldBe(500 + 500 + 600);
        total.InputAudioTokens.ShouldBe(1_000 + 1_600 + 2_100);
        total.OutputAudioTokens.ShouldBe(950);
    }

    [Fact]
    public void A_response_that_reports_no_usage_contributes_nothing()
    {
        // Rate-limit rejections and barge-in cancellations arrive with a null usage; the caller
        // should not have to guard against that at every call site.
        var tally = new RealtimeUsageTally();

        tally.Add(Response(100, 200, 0, 0, 10, 20));
        tally.Add(null);

        tally.Total.TotalTokens.ShouldBe(330);
    }

    [Fact]
    public void A_cancelled_response_is_still_paid_for()
    {
        // Barge-in cuts the reply short, but the conversation was still sent as input and the
        // audio generated so far was still produced. Dropping these was worth roughly a fifth
        // of a call on a chatty line.
        var tally = new RealtimeUsageTally();

        tally.Add(Response(1_000, 3_000, 0, 0, 20, 150));

        tally.Total.OutputAudioTokens.ShouldBe(150);
        tally.Total.InputAudioTokens.ShouldBe(3_000);
    }

    [Fact]
    public void Usage_with_no_modality_breakdown_falls_back_to_the_headline_input_count()
    {
        // A provider (or a future API version) that omits the details would otherwise record a
        // call as having consumed no input at all. Attributed to text, the cheaper input, so an
        // unknown mix understates spend rather than inventing it.
        var tally = new RealtimeUsageTally();

        tally.Add(Usage("""
        { "total_tokens": 4200, "input_tokens": 4000, "output_tokens": 200 }
        """));

        var total = tally.Total;
        total.InputTextTokens.ShouldBe(4_000);
        total.InputAudioTokens.ShouldBe(0);
        total.TotalTokens.ShouldBe(4_000);
    }

    [Fact]
    public void A_flat_cached_count_with_no_split_is_still_discounted()
    {
        var tally = new RealtimeUsageTally();

        tally.Add(Usage("""
        {
          "total_tokens": 3000, "input_tokens": 3000, "output_tokens": 0,
          "input_token_details": { "text_tokens": 3000, "audio_tokens": 0, "cached_tokens": 1200 }
        }
        """));

        var total = tally.Total;
        total.InputTextTokens.ShouldBe(1_800);
        total.CachedInputTextTokens.ShouldBe(1_200);
    }

    [Fact]
    public void Concurrent_responses_all_land_in_the_total()
    {
        // Tool follow-ups run off the event loop, so Add can be reached from more than one
        // thread; a lost update here would silently undercount the bill.
        var tally = new RealtimeUsageTally();

        Parallel.For(0, 64, index => tally.Add(Response(10, 20, 0, 0, 1, 2)));

        tally.Total.TotalTokens.ShouldBe(64 * 33);
    }
}
