#pragma warning disable OPENAI002 // OpenAI.Realtime is still an evolving/preview surface of the OpenAI SDK.
using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Realtime;

namespace Secretary.Agents.Realtime;

/// <summary>One persistent session against OpenAI's Realtime API, for the lifetime of a
/// single "call." Built on the official OpenAI SDK's RealtimeClient rather than a hand-rolled
/// ClientWebSocket — the Realtime wire protocol went through a breaking Beta-to-GA schema
/// change (event names renamed, session config restructured under audio.input/audio.output)
/// that a hand-rolled client would need to track manually. Tool calls reuse the exact same
/// AIFunction objects as the text-based path (see PhoneAgentToolset) — only the transport
/// differs.</summary>
public sealed class RealtimeVoiceSession : IAsyncDisposable
{
    private readonly RealtimeOptions _options;
    private readonly IReadOnlyList<AIFunction> _functions;

    // The orchestrator drives two relay loops against this one session concurrently — inbound
    // audio chunks streaming in continuously, and outbound function-call results firing whenever
    // a tool completes. The underlying WebSocket only supports one send in flight at a time;
    // without this, those two loops can write to it at the same instant, interleaving/corrupting
    // frames. That showed up as dropped/garbled audio and, downstream, the model losing track of
    // which language it was mid-conversation in once a corrupted turn came through.
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ILogger<RealtimeVoiceSession> _logger;
    private RealtimeSessionClient? _session;

    public RealtimeVoiceSession(IOptions<RealtimeOptions> options, IList<AITool> tools, ILogger<RealtimeVoiceSession> logger)
    {
        _options = options.Value;
        _functions = tools.OfType<AIFunction>().ToList();
        _logger = logger;
    }

    /// <summary>The model actually used for this call. Overridable so the full and mini realtime
    /// models can be dialled as separate pipelines and compared on the same line — mini's audio
    /// tokens cost about a third of the full model's, and whether Azerbaijani survives the
    /// smaller model is a question only a real call answers.</summary>
    public string Model { get; private set; } = string.Empty;

    public Task ConnectAsync(string instructions, CancellationToken cancellationToken)
        => ConnectAsync(instructions, null, cancellationToken);

    public async Task ConnectAsync(string instructions, string? modelOverride, CancellationToken cancellationToken)
    {
        Model = string.IsNullOrWhiteSpace(modelOverride) ? _options.Model : modelOverride;

        var client = new OpenAIClient(new ApiKeyCredential(_options.ApiKey));
        var realtimeClient = client.GetRealtimeClient();

        // The ApiKeyCredential above only wires the OpenAIClient's own HTTP pipeline (REST
        // calls) — it is NOT automatically propagated into the WebSocket handshake the
        // Realtime session opens, which is a separate connection entirely. Without this
        // explicit header, OpenAI accepts the connection but then rejects every request over
        // it with "Missing bearer or basic authentication in header" — no exception locally,
        // just a server-sent error event once the session tries to do anything.
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
                    // Far field, not near field: callers are on speakerphone or a laptop mic,
                    // so the microphone hears the room — and, worst of all, Lamiya's own voice
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
                        // speaking" is far more expensive than a slightly late one: it kills
                        // the in-flight response outright (status cancelled, reason
                        // turn_detected) and the caller hears nothing at all. The longer
                        // silence window also stops a mid-sentence breath from ending a turn.
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

        foreach (var tool in RealtimeToolSchema.FromTools(_functions))
        {
            sessionOptions.Tools.Add(tool);
        }

        await Session.ConfigureConversationSessionAsync(sessionOptions, cancellationToken);
    }

    /// <summary>PCM16 audio captured from the caller's microphone (the browser Call page) —
    /// the SDK handles base64 wire encoding internally.</summary>
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

    public IAsyncEnumerable<RealtimeServerUpdate> ReceiveUpdatesAsync(CancellationToken cancellationToken)
        => Session.ReceiveUpdatesAsync(cancellationToken);

    /// <summary>Called once, right after connecting, so Lamiya greets the caller and gives the
    /// recording notice proactively (Flow A/D) instead of sitting silently until the caller
    /// speaks first.</summary>
    public Task StartResponseAsync(CancellationToken cancellationToken)
        => StartResponseAsync(null, cancellationToken);

    /// <summary>With <paramref name="instructions"/>, this one response is generated under them
    /// INSTEAD of the session's — the model is told what to say for a single turn and nothing
    /// else applies. Reserved for the few turns whose wording is not the model's to choose: a
    /// rule buried in a long prompt keeps losing to the model's urge to narrate itself
    /// ("o zaman qısa şəkildə yekunlaşdırım"), whereas a one-line instruction covering one turn
    /// has nothing to compete with. Tools are off for those turns too — there is nothing left
    /// to look up once the wording is dictated.</summary>
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

    /// <summary>Barge-in: the caller started talking again while Lamiya's previous response was
    /// still being generated/played. Cancels that response server-side so the model isn't left
    /// completing a reply nobody's listening to anymore while also starting a new one — that's
    /// what produced overlapping audio, and plausibly the language drift too (the model was
    /// effectively mid-two-turns at once).</summary>
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

    /// <summary>Invoked when a function-call-arguments-done update arrives — missing this
    /// means a tool call silently never executes, which on a live call is far worse than in a
    /// text chat, so the orchestrator must not skip it.</summary>
    /// <summary>A caller on a live line can't be left hanging while a slow database call drags
    /// on. Past this, the operation is abandoned and the call is routed to a human instead.</summary>
    private static readonly TimeSpan ToolCallTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The model has no clock, so it can't know a check felt slow to the caller —
    /// past this, the tool output gets a note telling it to thank the caller for waiting.</summary>
    private static readonly TimeSpan LongWaitThreshold = TimeSpan.FromSeconds(5);

    /// <summary>Runs the tool and returns its output. Deliberately does NOT touch the session:
    /// feeding the result back and asking for the next response are separate steps the
    /// orchestrator sequences against the response lifecycle (see its turn-state comments) —
    /// doing all three here is what used to leave callers listening to silence.</summary>
    public async Task<string> ExecuteFunctionAsync(string functionName, string argumentsJson, CancellationToken cancellationToken)
    {
        // Logged verbatim on purpose — when a live call misbehaves ("that slot is taken",
        // wrong provider, wrong time), the exact arguments the model sent are the evidence.
        _logger.LogInformation("Tool call: {Function}({Arguments})", functionName, argumentsJson);

        var function = _functions.FirstOrDefault(f => f.Name == functionName);
        string output;

        if (function is null)
        {
            output = $"Unknown tool '{functionName}'.";
        }
        else
        {
            try
            {
                var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson) ?? new();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var (completedInTime, result) = await InvokeWithTimeoutAsync(function, new AIFunctionArguments(arguments), cancellationToken);
                output = completedInTime
                    ? result?.ToString() ?? string.Empty
                    : await EscalateAfterTimeoutAsync(functionName, cancellationToken);
                if (completedInTime && stopwatch.Elapsed > LongWaitThreshold)
                {
                    // A flag, not a sentence: prose addressed to the model gets spoken aloud.
                    // PhoneAgent.md says what SLOW_LOOKUP means for the reply.
                    output += " SLOW_LOOKUP.";
                }
            }
            catch (Exception ex)
            {
                output = $"Tool call failed: {ex.Message}";
            }
        }

        _logger.LogInformation("Tool result: {Function} -> {Output}", functionName, output);
        return output;
    }

    /// <summary>Feeds one function-call result back into the conversation. Adding the item does
    /// not make the model speak — the orchestrator asks for that separately, once, after every
    /// result of the turn is in and the response that requested them has finished.</summary>
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

    /// <summary>The tool methods don't take a CancellationToken (their service calls use
    /// `default`), so cancellation can't interrupt the underlying work — Task.WhenAny is the
    /// only way to stop waiting. The abandoned task keeps running to completion in the
    /// background; its eventual fault (if any) is observed so it can't surface as an
    /// unobserved-task exception.</summary>
    private static async Task<(bool CompletedInTime, object? Result)> InvokeWithTimeoutAsync(
        AIFunction function, AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var invokeTask = function.InvokeAsync(arguments, cancellationToken).AsTask();
        var winner = await Task.WhenAny(invokeTask, Task.Delay(ToolCallTimeout, cancellationToken));
        if (winner != invokeTask)
        {
            _ = invokeTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            return (false, null);
        }

        return (true, await invokeTask);
    }

    /// <summary>Product decision: anything the system can't do within 10 seconds on a live call
    /// gets routed to a human rather than making the caller wait. The escalation is raised here
    /// directly (not left to the model to decide), then the model is told what already happened
    /// so it can say the right thing.</summary>
    private async Task<string> EscalateAfterTimeoutAsync(string timedOutFunction, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Tool {Function} exceeded the {Seconds}s live-call timeout — escalating to a human.",
            timedOutFunction, ToolCallTimeout.TotalSeconds);

        var escalate = _functions.FirstOrDefault(f => f.Name == "EscalateToHuman");
        if (escalate is not null)
        {
            try
            {
                var arguments = new AIFunctionArguments(new Dictionary<string, object?>
                {
                    ["callerPhoneNumber"] = "unknown — ask the caller",
                    ["reason"] = $"System timeout: {timedOutFunction} did not complete within {ToolCallTimeout.TotalSeconds:0} seconds on a live call.",
                });
                var (completedInTime, _) = await InvokeWithTimeoutAsync(escalate, arguments, cancellationToken);
                if (completedInTime)
                {
                    return "TRANSFER_ALREADY_STARTED: the system timed out and has transferred the caller.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic escalation after a tool timeout failed.");
            }
        }

        return "TRANSFER_FAILED: the system timed out and could not transfer the caller automatically.";
    }

    private RealtimeSessionClient Session => _session ?? throw new InvalidOperationException("ConnectAsync must be called before using the session.");

    public ValueTask DisposeAsync()
    {
        _session?.Dispose();
        _sendLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
