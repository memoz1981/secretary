#pragma warning disable OPENAI002 // OpenAI.Realtime is still an evolving/preview surface of the OpenAI SDK.
using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Realtime;
using Secretary.Voice.Abstractions;

namespace Secretary.Voice.OpenAi;

/// <summary>One persistent session against OpenAI's Realtime API, for the lifetime of a single
/// call. Built on the official OpenAI SDK's RealtimeClient rather than a hand-rolled
/// ClientWebSocket — the Realtime wire protocol went through a breaking Beta-to-GA schema change
/// (event names renamed, session config restructured under audio.input/audio.output) that a
/// hand-rolled client would need to track manually.</summary>
public sealed class OpenAiRealtimeSession : IRealtimeSession
{
    private readonly OpenAiRealtimeOptions _options;
    private readonly IReadOnlyList<AIFunction> _functions;

    // The orchestrator drives two relay loops against this one session concurrently — inbound
    // audio chunks streaming in continuously, and outbound function-call results firing whenever
    // a tool completes. The underlying WebSocket only supports one send in flight at a time;
    // without this, those two loops can write to it at the same instant, interleaving/corrupting
    // frames. That showed up as dropped/garbled audio and, downstream, the model losing track of
    // which language it was mid-conversation in once a corrupted turn came through.
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ILogger<OpenAiRealtimeSession> _logger;
    private RealtimeSessionClient? _session;

    public OpenAiRealtimeSession(IOptions<OpenAiRealtimeOptions> options, IList<AITool> tools, ILogger<OpenAiRealtimeSession> logger)
    {
        _options = options.Value;
        _functions = tools.OfType<AIFunction>().ToList();
        _logger = logger;
    }

    public string Model { get; private set; } = string.Empty;

    /// <summary>OpenAI takes response.cancel, so barge-in is explicit here.</summary>
    public bool SupportsExplicitCancel => true;

    /// <summary>Adding a function-call output does not make the model speak; response.create
    /// does, and the orchestrator sends it once every tool of the turn has reported.</summary>
    public bool ContinuesTurnAfterToolResult => false;

    public async Task ConnectAsync(string instructions, string? modelOverride, CancellationToken cancellationToken)
    {
        Model = string.IsNullOrWhiteSpace(modelOverride) ? _options.Model : modelOverride;

        var client = new OpenAIClient(new ApiKeyCredential(_options.ApiKey));
        var realtimeClient = client.GetRealtimeClient();

        // The ApiKeyCredential above only wires the OpenAIClient's own HTTP pipeline (REST
        // calls) — it is NOT automatically propagated into the WebSocket handshake the Realtime
        // session opens, which is a separate connection entirely. Without this explicit header,
        // OpenAI accepts the connection but then rejects every request over it with "Missing
        // bearer or basic authentication in header" — no exception locally, just a server-sent
        // error event once the session tries to do anything.
        var sessionClientOptions = new RealtimeSessionClientOptions
        {
            Headers = { ["Authorization"] = $"Bearer {_options.ApiKey}" },
        };
        _session = await realtimeClient.StartConversationSessionAsync(Model, sessionClientOptions, cancellationToken);

        var sessionOptions = new RealtimeConversationSessionOptions
        {
            Instructions = instructions,
            AudioOptions = new RealtimeConversationSessionAudioOptions
            {
                InputAudioOptions = new RealtimeConversationSessionInputAudioOptions
                {
                    // Far field, not near field: callers are on speakerphone or a laptop mic, so
                    // the microphone hears the room — and, worst of all, Lamiya's own voice
                    // coming back out of the speakers. Left unfiltered that echo reads as the
                    // caller starting to talk, which cancels the reply being generated. See
                    // TurnDetection below for the other half of that fix.
                    NoiseReduction = new RealtimeNoiseReduction(RealtimeNoiseReductionKind.FarField),

                    // Transcribing the caller pins every one of their turns into the
                    // conversation as Azerbaijani TEXT. Without it the model has only audio to
                    // infer language from, and after a barge-in — a half-second of clipped
                    // speech — it kept guessing English ("How can I help you today?"). It also
                    // makes calls debuggable: the logs finally show what the caller said.
                    AudioTranscriptionOptions = new RealtimeAudioTranscriptionOptions
                    {
                        Model = "whisper-1",
                        Language = "az",
                    },

                    TurnDetection = new RealtimeServerVadTurnDetection
                    {
                        // Defaults are 0.5 / 500ms. Raised because a false "the caller is
                        // speaking" is far more expensive than a slightly late one: it kills the
                        // in-flight response outright (status cancelled, reason turn_detected)
                        // and the caller hears nothing at all. The longer silence window also
                        // stops a mid-sentence breath from ending a turn.
                        DetectionThreshold = 0.6f,
                        PrefixPadding = TimeSpan.FromMilliseconds(300),
                        SilenceDuration = TimeSpan.FromMilliseconds(700),
                    },
                },
                OutputAudioOptions = new RealtimeConversationSessionOutputAudioOptions
                {
                    Voice = RealtimeVoice.Marin,
                },
            },
            OutputModalities = { RealtimeOutputModality.Audio },
            ToolChoice = RealtimeDefaultToolChoice.Auto,

            // Every response is billed the whole conversation so far plus whatever it might
            // generate, against a per-minute token budget. Left unbounded, the "might generate"
            // part is reserved at the model's maximum, and mid-call that reservation is what
            // tips a tenant over the limit — the response is then rejected outright and the
            // caller hears nothing at all. Lamiya's turns are one or two sentences, so a modest
            // ceiling costs nothing real and keeps each request small.
            MaxOutputTokenCount = new RealtimeMaxOutputTokenCount(1200),
        };

        foreach (var tool in OpenAiRealtimeToolSchema.FromTools(_functions))
        {
            sessionOptions.Tools.Add(tool);
        }

        await Session.ConfigureConversationSessionAsync(sessionOptions, cancellationToken);
    }

    public async Task SendAudioChunkAsync(ReadOnlyMemory<byte> pcm16Chunk, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await Session.SendInputAudioAsync(BinaryData.FromBytes(pcm16Chunk.ToArray()), cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Translates OpenAI's update stream into neutral events. Updates the call loop
    /// never reacted to are dropped here rather than travelling further as a case nobody
    /// handles.</summary>
    public async IAsyncEnumerable<RealtimeEvent> ReceiveEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var update in Session.ReceiveUpdatesAsync(cancellationToken))
        {
            var translated = Translate(update);
            if (translated is not null)
            {
                yield return translated;
            }
        }
    }

    private static RealtimeEvent? Translate(RealtimeServerUpdate update) => update switch
    {
        RealtimeServerUpdateResponseCreated
            => new RealtimeEvent.ResponseStarted(),

        RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted transcription
            => new RealtimeEvent.CallerTranscript(transcription.Transcript?.Trim()),

        RealtimeServerUpdateConversationItemInputAudioTranscriptionFailed failure
            => new RealtimeEvent.CallerTranscriptFailed(failure.Error?.Message),

        RealtimeServerUpdateResponseOutputAudioDelta delta
            => new RealtimeEvent.AudioOut(delta.Delta.ToArray()),

        RealtimeServerUpdateResponseFunctionCallArgumentsDone call
            => new RealtimeEvent.ToolCallRequested(call.CallId, call.FunctionName, call.FunctionArguments.ToString()),

        RealtimeServerUpdateResponseDone done
            => new RealtimeEvent.ResponseFinished(
                ToOutcome(done.Response?.Status),
                done.Response?.StatusDetails?.Reason?.ToString(),
                done.Response?.Status == RealtimeResponseStatus.Failed
                    ? done.Response?.StatusDetails?.Error?.Message
                    : null,
                OpenAiUsageTranslator.ToTokenUsage(done.Response?.Usage)),

        RealtimeServerUpdateInputAudioBufferSpeechStarted
            => new RealtimeEvent.CallerSpeechStarted(),

        RealtimeServerUpdateInputAudioBufferSpeechStopped
            => new RealtimeEvent.CallerSpeechStopped(),

        RealtimeServerUpdateError error
            => new RealtimeEvent.SessionError(error.Error.Code, error.Error.Message),

        _ => null,
    };

    private static RealtimeResponseOutcome ToOutcome(RealtimeResponseStatus? status)
    {
        if (status == RealtimeResponseStatus.Completed) return RealtimeResponseOutcome.Completed;
        if (status == RealtimeResponseStatus.Cancelled) return RealtimeResponseOutcome.Cancelled;
        if (status == RealtimeResponseStatus.Failed) return RealtimeResponseOutcome.Failed;
        return RealtimeResponseOutcome.Incomplete;
    }

    public async Task StartResponseAsync(string? instructions, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (instructions is null)
            {
                await Session.StartResponseAsync(cancellationToken);
                return;
            }

            // This one response is generated under these instructions INSTEAD of the session's,
            // with tools off — reserved for the few turns whose wording is not the model's to
            // choose. A rule buried in a long prompt keeps losing to the model's urge to narrate
            // itself; a one-line instruction covering one turn has nothing to compete with.
            await Session.StartResponseAsync(
                new RealtimeResponseOptions
                {
                    Instructions = instructions,
                    ToolChoice = RealtimeDefaultToolChoice.None,
                },
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Barge-in: cancels the in-flight response server-side so the model isn't left
    /// completing a reply nobody's listening to anymore while also starting a new one — that's
    /// what produced overlapping audio, and plausibly the language drift too.</summary>
    public async Task CancelResponseAsync(CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await Session.CancelResponseAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task AddFunctionOutputAsync(string callId, string output, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await Session.AddItemAsync(RealtimeItem.CreateFunctionCallOutputItem(callId, output), cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private RealtimeSessionClient Session => _session ?? throw new InvalidOperationException("ConnectAsync must be called before using the session.");

    public ValueTask DisposeAsync()
    {
        _session?.Dispose();
        _sendLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
