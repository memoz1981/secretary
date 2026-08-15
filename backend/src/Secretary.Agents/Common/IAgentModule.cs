using Microsoft.Extensions.AI;
using Secretary.Domain.Enums;

namespace Secretary.Agents;

/// <summary>What one module gives the phone agent: the tools it can call, and the instructions
/// it answers by.
///
/// One phone line per module, decided rather than inherited. A tenant holding both Appointment
/// and Orders has two numbers, and a call answers as exactly one of them — so the agent never
/// has to work out mid-conversation which business it is in, and an order call can stay as terse
/// as an order call should be without carrying the appointment rules along with it.
///
/// This is design/architecture.md §4's IAgentModule in the thin form it actually needs: a
/// lookup, not a plugin system.</summary>
public interface IAgentModule
{
    Module Key { get; }

    /// <summary>Instruction file prefix; the provider suffix is appended. "Appointment" reads
    /// Appointment.openai.md or Appointment.gemini.md — one complete file per (module ×
    /// provider), per decision F3.</summary>
    string InstructionName { get; }

    /// <summary>Which pipelines may answer this module's line, or null for any of them.
    ///
    /// A module needs one complete instruction file per provider it can be dialled on, and the
    /// files are not interchangeable — answering a Gemini call with OpenAI-tuned instructions
    /// looks like it worked and quietly poisons every comparison between them. So a module
    /// written for one provider says so here, and both the startup guard and the endpoint read
    /// it: the guard stops asking for a file that should not exist, and the endpoint refuses the
    /// call rather than trusting a picker.</summary>
    IReadOnlyCollection<CallPipeline>? SupportedPipelines => null;

    /// <summary>Whether this module needs the caller's words written down.
    ///
    /// A property of the module, not a tenant preference and not one switch for the deployment.
    /// A feedback survey records open answers verbatim and is worthless without it. An order
    /// line already has everything it needs in the tool arguments, and transcription sits on the
    /// critical path between the caller finishing and the agent starting — so it pays latency
    /// for a recording nobody reads.
    ///
    /// False by default: a module that has not thought about it should not be charged for it.</summary>
    bool RequiresCallerTranscription => false;

    /// <summary>Anything about THIS call the agent should know before it speaks, appended to the
    /// instructions.
    ///
    /// Worth a seam of its own because the alternative is a tool call, and a tool call is two
    /// model invocations carrying the whole prompt — measured at roughly 4,700 tokens and a
    /// second of silence. A survey knowing whose name to open with should not cost that.
    ///
    /// Null for a module with nothing to add, which is most of them: an inbound line does not
    /// know who is ringing until it asks.</summary>
    Task<string?> BuildCallContextAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    /// <summary>The tools for one call. Built per call rather than held, because every tool
    /// object closes over request-scoped services.</summary>
    IList<AITool> BuildTools();

    /// <summary>Writes this call to the module's own log, once, as the call tears down.
    ///
    /// The module does it rather than the orchestrator because the record is module-shaped: an
    /// appointment call points at an appointment and a client in app.Calls, an order call at an
    /// order and a customer in ord.Calls. The orchestrator knows what every call has in common
    /// and nothing else, which is exactly what CallLogEntry carries — the module fills in who
    /// the call was about, because only its own tools ever found that out.</summary>
    Task LogCallAsync(CallLogEntry entry, CancellationToken cancellationToken);
}
