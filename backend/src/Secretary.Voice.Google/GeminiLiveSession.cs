using System.Runtime.CompilerServices;
using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Secretary.Voice.Abstractions;

namespace Secretary.Voice.Google;

/// <summary>One Gemini Live session, for the lifetime of a single call.
///
/// Same lifecycle as the OpenAI session; a different wire protocol underneath. Three
/// differences are real enough to be worth naming, and all three are absorbed here rather than
/// leaking into the call loop:
///
/// 1. Receive is a pull loop returning one message at a time, not a stream. Wrapped back into
///    IAsyncEnumerable, and one message can carry several events.
/// 2. There is no response.create and no response.done. Gemini generates whenever it has input.
///    ResponseStarted and ResponseFinished are synthesised from the first model content of a
///    turn and from TurnComplete.
/// 3. Sending a tool result automatically continues the turn, where OpenAI needs to be asked
///    separately. Declared through ContinuesTurnAfterToolResult so the orchestrator simply does
///    not ask — it has to know, because on the hang-up path the automatic turn is the farewell
///    rather than something racing it.</summary>
public sealed class GeminiLiveSession : IRealtimeSession
{
    /// <summary>Gemini resamples server-side from whatever the MIME type declares, so the
    /// browser's existing 24 kHz capture is sent unchanged rather than downsampled to Gemini's
    /// native 16 kHz. Its output is 24 kHz, which already matches what the page plays.</summary>
    private const string InputAudioMimeType = "audio/pcm;rate=24000";

    private readonly GeminiLiveOptions _options;
    private readonly IReadOnlyList<AIFunction> _functions;
    private readonly ILogger<GeminiLiveSession> _logger;

    // One send in flight at a time: the orchestrator relays inbound audio and tool results from
    // two loops concurrently, and a WebSocket does not tolerate interleaved writes.
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private Client? _client;
    private AsyncSession? _session;

    /// <summary>Whether a turn is currently open, so ResponseStarted fires once per turn rather
    /// than once per audio chunk.</summary>
    private bool _responseOpen;

    /// <summary>Usage arrives on its own message, typically just before TurnComplete. Held here
    /// so it can be attached to the ResponseFinished the loop actually acts on.</summary>
    private UsageMetadata? _pendingUsage;

    public GeminiLiveSession(IOptions<GeminiLiveOptions> options, IList<AITool> tools, ILogger<GeminiLiveSession> logger)
    {
        _options = options.Value;
        _functions = tools.OfType<AIFunction>().ToList();
        _logger = logger;
    }

    public string Model { get; private set; } = string.Empty;

    /// <summary>Gemini interrupts itself server-side and reports that it did. There is nothing
    /// to send, so barge-in cancellation is a no-op here.</summary>
    public bool SupportsExplicitCancel => false;

    /// <summary>Gemini resumes the turn the moment a tool result lands. The orchestrator reads
    /// this and skips its own follow-up rather than the session swallowing one — the difference
    /// matters on the hang-up path, where the orchestrator has to know that the automatic turn
    /// IS the farewell rather than something racing it.</summary>
    public bool ContinuesTurnAfterToolResult => true;

    public async Task ConnectAsync(string instructions, string? modelOverride, CancellationToken cancellationToken)
    {
        Model = string.IsNullOrWhiteSpace(modelOverride) ? _options.Model : modelOverride;

        _client = _options.Backend == GeminiBackend.Enterprise
            ? new Client(enterprise: true, project: _options.Project, location: _options.Location)
            : new Client(apiKey: _options.ApiKey);

        var config = new LiveConnectConfig
        {
            ResponseModalities = [Modality.Audio],

            SystemInstruction = new Content
            {
                Role = "user",
                Parts = [new Part { Text = instructions }],
            },

            SpeechConfig = new SpeechConfig
            {
                VoiceConfig = new VoiceConfig
                {
                    PrebuiltVoiceConfig = new PrebuiltVoiceConfig { VoiceName = _options.Voice },
                },
                LanguageCode = _options.LanguageCode,
            },

            // The same reasoning as the OpenAI session: transcribing the caller pins each of
            // their turns into the conversation as text, which is what stops the model drifting
            // out of Azerbaijani after a clipped barge-in — and it makes calls reviewable.
            InputAudioTranscription = new AudioTranscriptionConfig
            {
                // Pinned rather than auto-detected, for the same reason the OpenAI session pins
                // whisper to "az": left to guess on a half-second of clipped speech, recognisers
                // reach for English.
                LanguageCodes = [_options.LanguageCode],
            },

            MaxOutputTokens = _options.MaxOutputTokens,
        };

        var tools = GeminiLiveToolSchema.FromTools(_functions);
        if (tools.Count > 0)
        {
            config.Tools = tools;
        }

        _session = await _client.Live.ConnectAsync(Model, config, cancellationToken);
        _logger.LogInformation("Gemini Live session connected ({Model}, backend={Backend}).", Model, _options.Backend);
    }

    public async Task SendAudioChunkAsync(ReadOnlyMemory<byte> pcm16Chunk, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await Session.SendRealtimeInputAsync(
                new LiveSendRealtimeInputParameters
                {
                    Audio = new Blob { Data = pcm16Chunk.ToArray(), MimeType = InputAudioMimeType },
                },
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async IAsyncEnumerable<RealtimeEvent> ReceiveEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            LiveServerMessage? message;
            try
            {
                message = await Session.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            // Null is a graceful close — the call is over, not an error.
            if (message is null)
            {
                yield break;
            }

            foreach (var translated in Translate(message))
            {
                yield return translated;
            }
        }
    }

    private IEnumerable<RealtimeEvent> Translate(LiveServerMessage message)
    {
        // Held rather than emitted: it belongs to the ResponseFinished that follows.
        if (message.UsageMetadata is not null)
        {
            _pendingUsage = message.UsageMetadata;
        }

        if (message.GoAway is not null)
        {
            // Gemini caps a websocket at around ten minutes and warns before closing. Calls run
            // a couple of minutes, so this should not fire — if it does, the session needs
            // resumption handling rather than a silent hang-up.
            yield return new RealtimeEvent.SessionError("go_away", "Gemini is closing the session.");
        }

        var content = message.ServerContent;
        if (content is not null)
        {
            if (content.InputTranscription?.Text is { Length: > 0 } heard)
            {
                yield return new RealtimeEvent.CallerTranscript(heard.Trim());
            }

            if (content.Interrupted == true)
            {
                // Gemini reports that it interrupted itself; OpenAI reports that the caller
                // started speaking. Same moment, and the loop wants the same thing from it:
                // count the caller's turn and tell the browser to drop queued playback.
                // Emitted as a pair because there is no matching "stopped" message, and the
                // orchestrator's turn state expects begin and end to balance.
                yield return new RealtimeEvent.CallerSpeechStarted();
                yield return new RealtimeEvent.CallerSpeechStopped();

                if (_responseOpen)
                {
                    _responseOpen = false;
                    yield return new RealtimeEvent.ResponseFinished(
                        RealtimeResponseOutcome.Cancelled, "interrupted", null, TakePendingUsage());
                }
            }

            foreach (var part in content.ModelTurn?.Parts ?? [])
            {
                if (part.InlineData?.Data is not { Length: > 0 } audio)
                {
                    continue;
                }

                foreach (var started in OpenResponseIfNeeded())
                {
                    yield return started;
                }

                yield return new RealtimeEvent.AudioOut(audio);
            }

            if (content.TurnComplete == true && _responseOpen)
            {
                _responseOpen = false;
                yield return new RealtimeEvent.ResponseFinished(
                    RealtimeResponseOutcome.Completed,
                    content.TurnCompleteReason?.ToString(),
                    null,
                    TakePendingUsage());
            }
        }

        foreach (var call in message.ToolCall?.FunctionCalls ?? [])
        {
            foreach (var started in OpenResponseIfNeeded())
            {
                yield return started;
            }

            // Gemini hands arguments back as a dictionary; the invoker takes JSON, the same
            // shape OpenAI sends on the wire.
            var argumentsJson = JsonSerializer.Serialize(call.Args ?? []);

            // The id is what SendToolResponseAsync must echo. Gemini may omit it for
            // single-call turns, in which case the name is the only handle we have.
            yield return new RealtimeEvent.ToolCallRequested(call.Id ?? call.Name ?? string.Empty, call.Name ?? string.Empty, argumentsJson);
        }
    }

    private IEnumerable<RealtimeEvent> OpenResponseIfNeeded()
    {
        if (_responseOpen)
        {
            yield break;
        }

        _responseOpen = true;
        yield return new RealtimeEvent.ResponseStarted();
    }

    private Secretary.Domain.ValueObjects.TokenUsage? TakePendingUsage()
    {
        var usage = GeminiUsageTranslator.ToTokenUsage(_pendingUsage);
        _pendingUsage = null;
        return usage;
    }

    public async Task StartResponseAsync(string? instructions, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            // With no dictated wording this is the opening nudge, which the greeting needs:
            // Gemini stays silent until it has input, and the caller should hear the recording
            // notice before they speak.
            var turn = new Content
            {
                Role = "user",
                Parts = [new Part { Text = instructions ?? "Başla." }],
            };

            await Session.SendClientContentAsync(
                new LiveSendClientContentParameters { Turns = [turn], TurnComplete = true },
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>No-op: see <see cref="SupportsExplicitCancel"/>. Gemini has already stopped by
    /// the time it tells us it was interrupted.</summary>
    public Task CancelResponseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task AddFunctionOutputAsync(string callId, string output, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await Session.SendToolResponseAsync(
                new LiveSendToolResponseParameters
                {
                    FunctionResponses =
                    [
                        new FunctionResponse
                        {
                            Id = callId,
                            // Gemini wants a structured response, not a bare string. The tools
                            // return prose meant for the model, so it is passed under one key
                            // rather than invented into a schema.
                            Response = new Dictionary<string, object> { ["result"] = output },
                        },
                    ],
                },
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private AsyncSession Session => _session ?? throw new InvalidOperationException("ConnectAsync must be called before using the session.");

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            try
            {
                await _session.CloseAsync();
            }
            catch (Exception ex)
            {
                // Disposal must not throw over a socket that is already gone.
                _logger.LogDebug(ex, "Closing the Gemini Live session failed; it was most likely already closed.");
            }

            await _session.DisposeAsync();
        }

        _client?.Dispose();
        _sendLock.Dispose();
    }
}
