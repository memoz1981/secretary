using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Secretary.Agents.Realtime;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>The microphone-edge marker runs on every audio chunk inside the relay, so anything it
/// throws ends the call. It did: Math.Abs(short.MinValue) has no result, and one full-scale
/// negative sample — a loud moment at the caller's microphone — hung up on them mid-sentence,
/// which read as the model dropping calls at random.
///
/// These tests take the whole 16-bit range as input, since the point is that no PCM sample can
/// exist that this cannot measure.</summary>
public sealed class CallerAudioEdgeTests
{
    /// <summary>Debug level is what the marker checks before doing any work at all — against
    /// NullLogger it returns immediately and these tests would pass while measuring nothing.</summary>
    private static ILogger DebugLogger() => new DebugEnabledLogger();

    private sealed class DebugEnabledLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    [Fact]
    public void A_full_scale_negative_sample_is_measured_rather_than_thrown_on()
    {
        var pcm = Pcm(short.MinValue, short.MinValue, short.MinValue, short.MinValue);

        LiveVoiceCallOrchestrator.LogCallerAudioEdge(DebugLogger(), pcm, wasAudible: false).ShouldBeTrue();
    }

    [Theory]
    [InlineData(short.MinValue)]
    [InlineData(short.MinValue + 1)]
    [InlineData((short)-1)]
    [InlineData((short)0)]
    [InlineData((short)1)]
    [InlineData(short.MaxValue)]
    public void Every_extreme_of_the_sample_range_is_safe(short sample)
    {
        var pcm = Pcm(sample, sample);

        Should.NotThrow(() => LiveVoiceCallOrchestrator.LogCallerAudioEdge(DebugLogger(), pcm, wasAudible: false));
    }

    [Fact]
    public void Silence_reads_as_quiet_and_a_loud_chunk_as_speaking()
    {
        var logger = DebugLogger();

        LiveVoiceCallOrchestrator.LogCallerAudioEdge(logger, Pcm(0, 0, 0, 0), wasAudible: true).ShouldBeFalse();
        LiveVoiceCallOrchestrator.LogCallerAudioEdge(logger, Pcm(8000, -8000, 8000, -8000), wasAudible: false).ShouldBeTrue();
    }

    /// <summary>A chunk too short to hold one sample must not divide by zero.</summary>
    [Fact]
    public void A_chunk_below_one_whole_sample_leaves_the_state_alone()
    {
        LiveVoiceCallOrchestrator.LogCallerAudioEdge(DebugLogger(), new byte[1], wasAudible: true).ShouldBeTrue();
    }

    private static byte[] Pcm(params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 2, 2), samples[i]);
        }

        return bytes;
    }
}
