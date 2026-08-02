namespace Secretary.Voice.Abstractions;

/// <summary>One live speech-to-speech session, for the lifetime of a single call.
///
/// OpenAI Realtime and Gemini Live are the same shape underneath: open a socket, send session
/// config, stream audio in, receive audio and tool calls out, hand tool results back. The wire
/// formats differ entirely; the lifecycle does not.
///
/// Tool *execution* is deliberately absent — running an AIFunction has nothing to do with the
/// provider, so it lives in <see cref="RealtimeToolInvoker"/> and is shared.</summary>
public interface IRealtimeSession : IAsyncDisposable
{
    /// <summary>Which provider this is — "openai", "gemini". Used to pick the instruction file
    /// written for this model; the two mishear and misspeak differently enough that a shared
    /// file with patches was making one worse to fix the other.</summary>
    string ProviderKey { get; }

    /// <summary>The model actually used, resolved after Connect — the pipeline may override the
    /// configured default. Recorded on the call so cost can be attributed to a rate card.</summary>
    string Model { get; }

    /// <summary>False when interruption is handled entirely server-side and there is no cancel
    /// to send. Gemini Live is such a provider: it reports that it interrupted itself rather
    /// than accepting an instruction to stop. Callers must not treat a no-op cancel as a
    /// failure.</summary>
    bool SupportsExplicitCancel { get; }

    /// <summary>True when handing back a tool result makes the model speak again on its own.
    ///
    /// OpenAI does not: the result is added to the conversation and a reply must be asked for
    /// separately. Gemini does. The caller has to know which, because asking a provider that
    /// already answered produces two replies to one question — and, worse, on the hang-up path
    /// the automatic turn beats the dictated farewell and the call ends in silence.</summary>
    bool ContinuesTurnAfterToolResult { get; }

    Task ConnectAsync(string instructions, string? modelOverride, CancellationToken cancellationToken);

    /// <summary>PCM16 captured from the caller's microphone. Both providers accept the browser's
    /// 24 kHz: OpenAI natively, Gemini by resampling server-side from the declared rate.</summary>
    Task SendAudioChunkAsync(ReadOnlyMemory<byte> pcm16Chunk, CancellationToken cancellationToken);

    IAsyncEnumerable<RealtimeEvent> ReceiveEventsAsync(CancellationToken cancellationToken);

    /// <summary>Asks for a reply. With <paramref name="instructions"/>, that one turn is
    /// generated under them instead of the session's, with tools off — reserved for the few
    /// turns whose wording is not the model's to choose.</summary>
    Task StartResponseAsync(string? instructions, CancellationToken cancellationToken);

    /// <summary>Barge-in. A no-op where <see cref="SupportsExplicitCancel"/> is false.</summary>
    Task CancelResponseAsync(CancellationToken cancellationToken);

    /// <summary>Feeds one tool result back. Does not itself make the model speak — the caller
    /// sequences that against the response lifecycle.</summary>
    Task AddFunctionOutputAsync(string callId, string output, CancellationToken cancellationToken);
}
