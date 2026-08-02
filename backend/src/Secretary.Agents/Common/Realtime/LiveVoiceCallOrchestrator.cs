using System.Net.WebSockets;
using Secretary.Application.Dtos;
using Secretary.Application.Pricing;
using Secretary.Application.Services;
using Secretary.Domain.Enums;
using Microsoft.Extensions.Logging;
using NodaTime;
using Secretary.Voice;
using Secretary.Voice.Abstractions;

namespace Secretary.Agents.Realtime;

/// <summary>Bridges the browser's microphone/speaker (the Call page — see
/// frontend/src/lib/liveVoiceCall.ts) to OpenAI's Realtime API, replacing the originally
/// discussed Twilio Media Streams telephony path: instead of a real phone line, a person's
/// own computer audio devices "call" this business's AI agent directly. Everything else —
/// the 8 tools, the instructions, the call-logging discipline — is identical to the
/// text-based and (eventually) telephony paths.</summary>
public sealed class LiveVoiceCallOrchestrator
{
    /// <summary>No real caller phone number exists for a computer-device call — this is the
    /// deliberate placeholder identifier logged in its place (Call.CallerPhoneNumber is
    /// required and non-blank).</summary>
    private const string LocalDeviceCallerIdentifier = "local-device-call";

    private readonly RealtimeSessionResolver _sessionResolver;
    private readonly RealtimeToolInvoker _toolInvoker;
    private readonly CallService _callService;
    private readonly AgentInstructionContext _instructionContext;
    private readonly IClock _clock;
    private readonly ILogger<LiveVoiceCallOrchestrator> _logger;

    /// <summary>Assigned at the top of RunAsync, once the pipeline says which provider answers.
    /// Not injected, because which implementation is wanted is a per-call decision.</summary>
    private IRealtimeSession _realtimeSession = null!;

    public LiveVoiceCallOrchestrator(
        RealtimeSessionResolver sessionResolver, RealtimeToolInvoker toolInvoker, CallService callService,
        AgentInstructionContext instructionContext, IClock clock, ILogger<LiveVoiceCallOrchestrator> logger)
    {
        _sessionResolver = sessionResolver;
        _toolInvoker = toolInvoker;
        _callService = callService;
        _instructionContext = instructionContext;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>What this call has consumed so far. Written from the event loop only.</summary>
    private readonly RealtimeUsageTally _usage = new();

    /// <summary>Derived from what the call's successful tool calls actually did (last action
    /// wins); stays InquiryOther for calls where nothing was booked/changed/escalated.</summary>
    private CallClassification _classification = CallClassification.InquiryOther;

    /// <summary>Set when OpenAI sends a real error event (auth failure, server error — not the
    /// benign barge-in cancel race). Such errors don't throw; the relay loop just ends when the
    /// session closes, which used to log the call as ResolvedByAgent even though the caller
    /// never heard a word.</summary>
    private bool _sawErrorEvent;

    /// <summary>The call's question/answer count. Answers are replies the model actually
    /// completed — a response rejected by the rate limiter is not an answer, and counting it as
    /// one both flattered the "turns to resolution" KPI and divided the call's cost by a number
    /// bigger than the caller ever heard. Questions are the caller's own turns.</summary>
    private int _answerCount;
    private int _callerTurnCount;

    // ---- Turn state ----
    // A response.create is only legal once the previous response has finished. OpenAI emits
    // response.function_call_arguments.done BEFORE response.done, so asking for the follow-up
    // the moment a tool returned raced the still-active response: OpenAI rejected it, nothing
    // was generated, and the caller sat in silence until their own speech triggered the VAD —
    // the "it stops until I say something" symptom. Tools now run off the event loop (so audio
    // and barge-in keep flowing while the database works) and the follow-up is requested by
    // whichever finishes last: the response, or the tools it asked for.
    private readonly VoiceTurnState _turn = new();

    /// <summary>How long to wait before re-asking for a reply that produced nothing, and how
    /// many times. The delay is the point: an immediate retry lands in whatever condition
    /// silenced the first one and dies the same way — this gives OpenAI's own turn handling a
    /// moment to open a response first, in which case the retry stands down.</summary>
    private static readonly TimeSpan SilentResponseGrace = TimeSpan.FromMilliseconds(500);
    private const int MaxSilentRetries = 2;

    /// <summary>Rate-limit rejections come back as a failed response carrying the wait in prose
    /// ("Please try again in 6.73s"), not as a header we can read. Retrying before that has
    /// elapsed is guaranteed to fail again and spends another attempt doing it, which is how a
    /// one-off rejection turned into the caller sitting through silence.</summary>
    private static readonly System.Text.RegularExpressions.Regex RetryAfterPattern =
        new(@"try again in ([\d.]+)\s*(ms|s)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Ceiling on how long we'll hold the line waiting for a rate limit to clear. Past
    /// this the caller is better served by speaking again than by more silence.</summary>
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(10);

    private static TimeSpan RetryDelayFor(string? failureMessage)
    {
        if (failureMessage is null)
        {
            return SilentResponseGrace;
        }

        var match = RetryAfterPattern.Match(failureMessage);
        if (!match.Success || !double.TryParse(
                match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return SilentResponseGrace;
        }

        var stated = match.Groups[2].Value.Equals("ms", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromMilliseconds(value)
            : TimeSpan.FromSeconds(value);

        // A little past what it asked for: coming back at the exact boundary just fails again.
        var delay = stated + TimeSpan.FromMilliseconds(250);
        return delay > MaxRetryAfter ? MaxRetryAfter : delay;
    }

    private bool TryClaimFollowUp(string origin)
    {
        var claimed = _turn.TryClaimFollowUp(out var snapshot);
        if (claimed)
        {
            _logger.LogInformation("Turn[{Origin}]: starting follow-up response.", origin);
        }
        else
        {
            _logger.LogInformation(
                "Turn[{Origin}]: follow-up not started (responseActive={Active}, pendingTools={Pending}, needed={Needed}).",
                origin, snapshot.ResponseActive, snapshot.PendingToolCalls, snapshot.FollowUpNeeded);
        }

        return claimed;
    }

    public Task RunAsync(WebSocket clientSocket, CancellationToken cancellationToken)
        => RunAsync(clientSocket, CallPipeline.OpenAiRealtime_2_1, null, cancellationToken);

    /// <summary>Runs a realtime call on a specific model. The full and mini realtime models are
    /// the same architecture at very different prices, so they are dialled as separate pipelines
    /// and recorded as such — otherwise the Call Log could not tell them apart.</summary>
    public async Task RunAsync(
        WebSocket clientSocket, CallPipeline pipeline, string? modelOverride, CancellationToken cancellationToken)
    {
        var startedAt = _clock.GetCurrentInstant();
        var outcome = CallOutcome.ResolvedByAgent;

        // Which provider answers is the pipeline's decision, resolved here rather than injected.
        _realtimeSession = _sessionResolver.Create(pipeline);

        try
        {
            await _realtimeSession.ConnectAsync(
                _instructionContext.BuildPhoneAgentInstructions(), modelOverride, cancellationToken);

            var toOpenAi = RelayClientAudioToOpenAiAsync(clientSocket, cancellationToken);
            var toClient = RelayOpenAiEventsToClientAsync(clientSocket, cancellationToken);

            // Both relay loops are running before this fires, so nothing about the greeting's
            // own audio deltas gets missed — see Flow A/D: the caller should hear the recording
            // notice and a greeting proactively, not silence until they speak first.
            await _realtimeSession.StartResponseAsync(cancellationToken);

            // Either direction ending (caller hangs up / OpenAI closes) ends the call. Task.WhenAny
            // returns as soon as either task completes — including completing by faulting — but does
            // NOT rethrow that fault. Without awaiting the completed task, a relay that fails right
            // after connecting silently vanishes: no exception, no log, the finally block just closes
            // the socket looking like a normal end-of-call. Awaiting it here is what makes that
            // exception actually propagate into the catch block below.
            var completedRelay = await Task.WhenAny(toOpenAi, toClient);
            await completedRelay;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live voice call failed.");
            outcome = CallOutcome.FailedAgentLimitation;
        }
        finally
        {
            // Without this, the ASP.NET Core WebSocket endpoint's `using` block just disposes
            // the socket once RunAsync returns — that tears down the TCP connection without a
            // close frame, which .NET's ClientWebSocket on the other end (the console voice
            // client) surfaces as "closed the WebSocket connection without completing the
            // close handshake" instead of a clean end-of-call.
            if (clientSocket.State == WebSocketState.Open)
            {
                try
                {
                    var closeDescription = outcome == CallOutcome.ResolvedByAgent ? "Call ended" : "Call failed — see server logs";
                    await clientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, closeDescription, CancellationToken.None);
                }
                catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or OperationCanceledException)
                {
                    // The client may have already dropped the connection, or the underlying
                    // connection was already torn down by the framework (e.g. the request was
                    // aborted) — nothing to close gracefully at that point.
                }
            }

            var endedAt = _clock.GetCurrentInstant();
            var durationSeconds = (int)(endedAt - startedAt).TotalSeconds;

            // A call where the model never completed a single response and OpenAI reported an
            // error is a failed call, not a resolved one — the caller heard nothing.
            if (outcome == CallOutcome.ResolvedByAgent && _answerCount == 0 && _sawErrorEvent)
            {
                outcome = CallOutcome.FailedAgentLimitation;
            }

            var tokenUsage = _usage.Total;
            _logger.LogInformation(
                "Call finished: {Seconds}s, {Questions} questions, {Answers} answers, {Tokens} tokens "
                + "(in text/audio {InText}/{InAudio}, cached {CachedText}/{CachedAudio}, out text/audio {OutText}/{OutAudio}).",
                durationSeconds, _callerTurnCount, _answerCount, tokenUsage.TotalTokens,
                tokenUsage.InputTextTokens, tokenUsage.InputAudioTokens,
                tokenUsage.CachedInputTextTokens, tokenUsage.CachedInputAudioTokens,
                tokenUsage.OutputTextTokens, tokenUsage.OutputAudioTokens);

            // Deterministic, orchestration-driven logging — never left to the model to decide
            // to call, per the tool-list design (see backend/README.md's Agents section).
            // CancellationToken.None deliberately: this runs during call teardown, when the
            // request's own token is typically already cancelled (hang-up, client disconnect) —
            // passing it through meant every such call aborted its own Call Log write.
            try
            {
                await _callService.LogAsync(
                    new LogCallRequest(
                        LocalDeviceCallerIdentifier, null, _classification, outcome,
                        durationSeconds, _answerCount, _callerTurnCount, null,
                        "local-device-call (no recording stored)", null, startedAt, pipeline,
                        // One model does everything on this path — that is what the realtime
                        // API is. The chained pipelines report three entries here instead.
                        // Taken from the session, not from configuration, so a mini call is
                        // priced as mini rather than as whatever the default happens to be.
                        [new ModelUsage(_realtimeSession.Model, tokenUsage)]),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write the call log record for a live voice call.");
            }
        }
    }

    /// <summary>Success is recognized by each tool's own confirmation prefix — a failed or
    /// refused attempt ("that time is no longer available") must not reclassify the call.</summary>
    private void UpdateClassification(string functionName, string toolOutput)
    {
        // Assigned from a tool task on the thread pool, so it needs its own guard.
        lock (_classificationLock)
        {
            _classification = Classify(functionName, toolOutput);
        }
    }

    private readonly object _classificationLock = new();

    private CallClassification Classify(string functionName, string toolOutput)
    {
        return (functionName, toolOutput) switch
        {
            (nameof(Tools.AppointmentTools.BookAppointment), _) when toolOutput.StartsWith("Booked") => CallClassification.NewAppointment,
            (nameof(Tools.AppointmentTools.RescheduleAppointment), _) when toolOutput.StartsWith("Rescheduled") => CallClassification.UpdateReschedule,
            (nameof(Tools.AppointmentTools.CancelAppointment), _) when toolOutput.StartsWith("Cancelled") => CallClassification.Cancellation,
            _ => _classification,
        };
    }

    /// <summary>Runs one tool without blocking the event loop, feeds its result back, and asks
    /// for the model's reply if this was the last thing the turn was waiting on.</summary>
    private async Task RunToolCallAsync(string callId, string functionName, string argumentsJson, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var toolOutput = await _toolInvoker.ExecuteFunctionAsync(functionName, argumentsJson, cancellationToken);
            _logger.LogInformation("Turn: tool {Function} took {Ms} ms.", functionName, stopwatch.ElapsedMilliseconds);
            UpdateClassification(functionName, toolOutput);
            await _realtimeSession.AddFunctionOutputAsync(callId, toolOutput, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Without a result item the model would wait forever for this call; tell it plainly
            // so it can apologize or escalate instead of going silent.
            _logger.LogError(ex, "Tool {Function} failed on a live call.", functionName);
            try
            {
                await _realtimeSession.AddFunctionOutputAsync(callId, "LOOKUP_FAILED.", cancellationToken);
            }
            catch (Exception addEx)
            {
                _logger.LogError(addEx, "Could not report the failed tool call back to the model.");
            }
        }
        finally
        {
            var stillPending = _turn.EndToolCall();
            _logger.LogInformation("Turn: tool {Function} finished (pendingTools={Pending}).", functionName, stillPending);
        }

        try
        {
            // Capability first, so the claim is not taken and the log does not announce a
            // follow-up that never happens: a provider that resumes the turn on its own has
            // already started answering by the time the result lands.
            if (!_realtimeSession.ContinuesTurnAfterToolResult && TryClaimFollowUp("tool"))
            {
                await _realtimeSession.StartResponseAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // This task is fire-and-forget: an exception here would vanish silently and leave
            // the caller in exactly the dead air this whole mechanism exists to prevent.
            _logger.LogError(ex, "Failed to start the reply after tool {Function}.", functionName);
        }
    }

    /// <summary>An empty response is only OUR problem to fix when nobody meant it to be empty.
    /// The two cancellation reasons below are the system working as designed: the caller took
    /// the turn (their own turn produces the next response) or we cancelled on barge-in. Re-
    /// asking in those cases is what produced bursts of doomed 50-millisecond responses while
    /// the caller was still talking.</summary>
    private static bool ShouldRecoverFromSilence(string? reason)
        => !IsReason(reason, "turn_detected") && !IsReason(reason, "client_cancelled");

    /// <summary>Providers report the reason as a wire string, and the two in play here are
    /// OpenAI's. Matched loosely — with and without underscores, either casing — because the
    /// SDK's extensible-enum ToString() has given both "turn_detected" and "TurnDetected"
    /// across versions, and getting this wrong reintroduces the doomed-retry burst rather than
    /// failing loudly. A provider that reports no reason (Gemini) falls through to true, which
    /// is the right default: an unexplained silent turn IS worth re-asking.</summary>
    private static bool IsReason(string? reason, string wireName)
        => reason is not null
        && string.Equals(reason.Replace("_", string.Empty), wireName.Replace("_", string.Empty),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Re-asks for a reply that never came, but only if the wait didn't make the
    /// question moot — see <see cref="VoiceTurnState.CanRetrySilentResponse"/>.</summary>
    private async Task RetrySilentResponseAsync(int generation, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            if (!_turn.CanRetrySilentResponse(generation))
            {
                _logger.LogInformation("Turn: silent-response retry stood down — the call moved on without it.");
                return;
            }

            _logger.LogWarning("Turn: re-asking for the reply that produced nothing.");
            await _realtimeSession.StartResponseAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The call ended while we were waiting — nothing to recover.
        }
        catch (Exception ex)
        {
            // Fire-and-forget: an exception here would vanish silently and leave the caller in
            // exactly the dead air this exists to prevent.
            _logger.LogError(ex, "Failed to re-ask for a silent response.");
        }
    }

    private async Task RelayClientAudioToOpenAiAsync(WebSocket clientSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (clientSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await clientSocket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                await _realtimeSession.SendAudioChunkAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            }
        }
    }

    /// <summary>Sent as a WebSocket Text frame (audio itself is always Binary) to tell the
    /// client a barge-in happened and it should drop whatever it already queued for playback —
    /// see RealtimeVoiceSession.CancelResponseAsync's docstring.</summary>
    private const string BargeInSignal = "barge-in";

    /// <summary>The farewell is dictated rather than requested. Every softer version of this —
    /// a marker in a tool result, a rule in the instructions — was eventually read aloud as
    /// narration instead ("xətti bağlamaq üçün yekunlaşdırım", "o zaman qısa şəkildə
    /// yekunlaşdırım"). As this replaces the session instructions for that one turn, there is
    /// no surrounding guidance left for the model to comment on.</summary>
    private const string GoodbyeInstruction =
        "Say exactly: \"Sağ olun, görüşənədək!\" — nothing before it, nothing after it. " +
        "If and only if this call was held in Russian or English, say the same brief farewell in that " +
        "language instead. Two or three words. Never explain, summarise, or ask the caller to wait.";

    /// <summary>Sent as a WebSocket Text frame when the model calls EndCall: the client should
    /// stop capturing, let its queued goodbye audio finish playing, and close the connection.
    /// The client is the one that knows exactly how much audio is still queued, so the drain
    /// happens on its side, not by the server guessing a delay.</summary>
    private const string HangUpSignal = "hang-up";

    private async Task RelayOpenAiEventsToClientAsync(WebSocket clientSocket, CancellationToken cancellationToken)
    {
        // EndCall handling is two-phase because the model doesn't reliably bundle its goodbye
        // audio into the same response as the EndCall function call — in practice it often
        // sends a silent, function-call-only response and plans to speak after the tool
        // result. So: when the EndCall response finishes WITH audio, hang up right there
        // (goodbye already relayed); when it was silent, feed the function output back, let
        // the model speak one final goodbye response, and hang up when that one finishes.
        string? pendingEndCallId = null;
        var hangUpAtNextResponseDone = false;
        var audioInCurrentResponse = false;

        // A response can finish having produced nothing at all — no audio, no tool call. The
        // caller just hears silence, and the only thing that revives the call is them speaking,
        // which is exactly the "it waits until I say something" symptom. Detect that and ask
        // for another response; the retry is capped so a model that stays mute can't spin.
        var toolCallInCurrentResponse = false;
        var silentRetries = 0;
        var responseStarted = System.Diagnostics.Stopwatch.StartNew();
        var firstAudioLogged = false;
        var currentGeneration = 0;

        await foreach (var update in _realtimeSession.ReceiveEventsAsync(cancellationToken))
        {
            switch (update)
            {
                case RealtimeEvent.ResponseStarted:
                    currentGeneration = _turn.BeginResponse();
                    _logger.LogInformation("Turn: response created (pendingTools={Pending}).", _turn.PendingToolCalls);

                    audioInCurrentResponse = false;
                    toolCallInCurrentResponse = false;
                    firstAudioLogged = false;
                    responseStarted.Restart();
                    break;

                // Only logged, never acted on: the transcript is what makes a recorded call
                // reviewable afterwards, and it is how we can tell a real caller turn from the
                // microphone tripping over background noise or the agent's own voice.
                case RealtimeEvent.CallerTranscript transcription:
                    _logger.LogInformation("Caller said: {Transcript}", transcription.Text);
                    break;

                case RealtimeEvent.CallerTranscriptFailed transcriptionFailure:
                    // Not fatal: the model still has the audio itself. Worth knowing about,
                    // because losing the transcript is what weakens its grip on the language.
                    _logger.LogWarning("Caller transcription failed: {Message}", transcriptionFailure.Message);
                    break;

                case RealtimeEvent.AudioOut delta:
                    var bytes = delta.Pcm16;
                    if (bytes.Length > 0 && clientSocket.State == WebSocketState.Open)
                    {
                        if (!firstAudioLogged)
                        {
                            firstAudioLogged = true;
                            _logger.LogInformation("Turn: first audio after {Ms} ms.", responseStarted.ElapsedMilliseconds);
                        }

                        audioInCurrentResponse = true;
                        await clientSocket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken);
                    }

                    break;

                case RealtimeEvent.ToolCallRequested functionCall:
                    if (functionCall.Name == nameof(Tools.CallControlTools.EndCall))
                    {
                        _logger.LogInformation("Model requested EndCall — hanging up after its goodbye is spoken.");

                        if (_realtimeSession.ContinuesTurnAfterToolResult)
                        {
                            // Answered immediately, and the farewell wording rides along with
                            // the result.
                            //
                            // Gemini does not complete a turn while a tool call is outstanding,
                            // so deferring this to response-done deadlocks: the completion waits
                            // on the result and the result waits on the completion. The call
                            // then sat silent until something interrupted it — the reported
                            // "waits 5-10 seconds and hangs up with no goodbye". OpenAI emits
                            // response.done with a function call still unanswered, which is why
                            // deferring works there and only there.
                            await _realtimeSession.AddFunctionOutputAsync(
                                functionCall.CallId, $"CALL_ENDED. {GoodbyeInstruction}", cancellationToken);
                            hangUpAtNextResponseDone = true;
                        }
                        else
                        {
                            pendingEndCallId = functionCall.CallId;
                        }

                        break;
                    }

                    toolCallInCurrentResponse = true;
                    _turn.BeginToolCall();

                    // Deliberately not awaited: the loop must keep relaying audio and handling
                    // barge-in while the tool runs. RunToolCallAsync owns its own errors.
                    _ = RunToolCallAsync(
                        functionCall.CallId, functionCall.Name, functionCall.ArgumentsJson, cancellationToken);
                    break;

                case RealtimeEvent.ResponseFinished responseDone:
                    _turn.EndResponse();
                    var status = responseDone.Outcome;
                    var statusReason = responseDone.Reason;

                    // Billed whether or not the caller got anything useful out of it — a reply
                    // cut short by barge-in still consumed the whole conversation as input — so
                    // usage is tallied for every response that reports any, including the
                    // cancelled and failed ones.
                    _usage.Add(responseDone.Usage);
                    _logger.LogInformation(
                        "Turn: response done after {Ms} ms (status={Status}, reason={Reason}, audio={HadAudio}, toolCall={HadTool}, pendingTools={Pending}).",
                        responseStarted.ElapsedMilliseconds, status, statusReason, audioInCurrentResponse,
                        toolCallInCurrentResponse, _turn.PendingToolCalls);

                    var failureMessage = responseDone.FailureMessage;
                    if (failureMessage is not null)
                    {
                        // A failed response throws nothing and closes nothing — without this the
                        // call is logged as ResolvedByAgent even though the caller heard silence.
                        _sawErrorEvent = true;
                        _logger.LogError("Response generation failed: {Message}", failureMessage);
                    }

                    if (status == RealtimeResponseOutcome.Completed)
                    {
                        _answerCount++;
                    }

                    // Checked before the deferred-id branch and independently of it: the
                    // Gemini path answers EndCall the moment it is asked (see the tool-call
                    // case), so by the time its farewell finishes there is no pending id left
                    // to hang the check off.
                    if (hangUpAtNextResponseDone)
                    {
                        // The farewell has been fully relayed — tell the client to finish
                        // playing what it has queued and hang up, and end this relay loop,
                        // which ends the call.
                        if (clientSocket.State == WebSocketState.Open)
                        {
                            var hangUp = System.Text.Encoding.UTF8.GetBytes(HangUpSignal);
                            await clientSocket.SendAsync(hangUp, WebSocketMessageType.Text, true, cancellationToken);
                        }

                        return;
                    }

                    if (pendingEndCallId is not null)
                    {
                        // The farewell is not the model's line to compose, so it always gets
                        // said here — one final response whose instructions are only those
                        // words. This used to be skipped whenever the EndCall turn had produced
                        // audio of its own, on the assumption that the audio WAS the goodbye.
                        // It isn't: asked to say nothing and hang up, the model said "bir anlıq
                        // gözləyin" instead, and the call ended on "hold on a moment".
                        //
                        // Only reached for a provider that does not resume on its own; the
                        // other kind never sets a pending id.
                        await _realtimeSession.AddFunctionOutputAsync(pendingEndCallId, "CALL_ENDED.", cancellationToken);
                        await _realtimeSession.StartResponseAsync(GoodbyeInstruction, cancellationToken);
                        hangUpAtNextResponseDone = true;
                        break;
                    }

                    // Whatever finishes last — this response, or the tools it asked for — is
                    // what gets to ask the model to speak the result. Skipped entirely for a
                    // provider that resumes the turn on its own; see RunToolCallAsync.
                    if (!_realtimeSession.ContinuesTurnAfterToolResult && TryClaimFollowUp("response-done"))
                    {
                        await _realtimeSession.StartResponseAsync(cancellationToken);
                        break;
                    }

                    if (audioInCurrentResponse)
                    {
                        silentRetries = 0;
                        break;
                    }

                    // Nothing was said and nothing was called: the turn produced dead air.
                    // Whether that's worth fixing depends entirely on WHY it ended.
                    if (toolCallInCurrentResponse || !ShouldRecoverFromSilence(statusReason) || silentRetries >= MaxSilentRetries)
                    {
                        break;
                    }

                    silentRetries++;
                    var retryDelay = RetryDelayFor(failureMessage);
                    _logger.LogWarning(
                        "Turn: response produced no audio and no tool call (status={Status}, reason={Reason}) — will re-ask in {Ms} ms.",
                        status, statusReason, retryDelay.TotalMilliseconds);
                    _ = RetrySilentResponseAsync(currentGeneration, retryDelay, cancellationToken);
                    break;

                case RealtimeEvent.CallerSpeechStarted:
                    // A question, for the call's question/answer count. Server VAD is what
                    // decides the caller took the turn, which is the same signal the model
                    // itself acts on — so this counts exactly the turns the model responded to.
                    _callerTurnCount++;

                    if (_turn.BeginCallerSpeech())
                    {
                        await _realtimeSession.CancelResponseAsync(cancellationToken);
                    }

                    // Always tell the client to drop its queued playback, even when generation
                    // already finished: audio generates several times faster than it plays, so
                    // by the time the caller talks over a reply, ResponseDone has usually long
                    // since arrived and the rest of that reply is sitting in the client's
                    // buffer. Skipping the clear here is what made Lamiya keep talking over
                    // the caller for the remainder of a long reply.
                    if (clientSocket.State == WebSocketState.Open)
                    {
                        var signal = System.Text.Encoding.UTF8.GetBytes(BargeInSignal);
                        await clientSocket.SendAsync(signal, WebSocketMessageType.Text, true, cancellationToken);
                    }

                    break;

                case RealtimeEvent.CallerSpeechStopped:
                    _turn.EndCallerSpeech();
                    break;

                case RealtimeEvent.SessionError error:
                    if (error.Code == "response_cancel_not_active")
                    {
                        // Benign race: with server VAD, OpenAI often auto-cancels the active
                        // response on speech-started before our explicit cancel lands. Not a
                        // problem worth a warning on every barge-in.
                        _logger.LogDebug("Barge-in cancel raced OpenAI's own auto-cancel (response_cancel_not_active).");
                        break;
                    }

                    _sawErrorEvent = true;
                    _logger.LogWarning("Realtime API error event: {Code} {Message}", error.Code, error.Message);
                    break;
            }
        }
    }
}
