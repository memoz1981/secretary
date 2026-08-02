using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Secretary.Voice;

/// <summary>Runs the tools a realtime model asks for. Provider-agnostic on purpose: invoking an
/// AIFunction has nothing to do with which model requested it, and both providers hand over the
/// same thing — a name and a JSON argument blob.
///
/// Deliberately does NOT touch the session. Feeding the result back and asking for the next
/// response are separate steps the orchestrator sequences against the response lifecycle; doing
/// all three here is what used to leave callers listening to silence.</summary>
public sealed class RealtimeToolInvoker
{
    /// <summary>A caller on a live line can't be left hanging while a slow database call drags
    /// on. Past this, the operation is abandoned and the call is routed to a human instead.</summary>
    private static readonly TimeSpan ToolCallTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The model has no clock, so it can't know a check felt slow to the caller — past
    /// this, the tool output gets a note telling it to thank the caller for waiting.</summary>
    private static readonly TimeSpan LongWaitThreshold = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyList<AIFunction> _functions;
    private readonly ILogger<RealtimeToolInvoker> _logger;

    public RealtimeToolInvoker(IList<AITool> tools, ILogger<RealtimeToolInvoker> logger)
    {
        _functions = tools.OfType<AIFunction>().ToList();
        _logger = logger;
    }

    public async Task<string> ExecuteFunctionAsync(string functionName, string argumentsJson, CancellationToken cancellationToken)
    {
        // Logged verbatim on purpose — when a live call misbehaves ("that slot is taken", wrong
        // provider, wrong time), the exact arguments the model sent are the evidence.
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

    /// <summary>The tool methods don't take a CancellationToken (their service calls use
    /// `default`), so cancellation can't interrupt the underlying work — Task.WhenAny is the only
    /// way to stop waiting. The abandoned task keeps running to completion in the background; its
    /// eventual fault (if any) is observed so it can't surface as an unobserved-task
    /// exception.</summary>
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
}
