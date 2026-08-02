namespace Secretary.Voice.Abstractions;

public static class RealtimeSessionExtensions
{
    /// <summary>Ask for a reply under the session's own instructions — the ordinary case.</summary>
    public static Task StartResponseAsync(this IRealtimeSession session, CancellationToken cancellationToken)
        => session.StartResponseAsync(null, cancellationToken);
}
