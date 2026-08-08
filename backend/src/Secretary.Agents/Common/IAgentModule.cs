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

    /// <summary>The tools for one call. Built per call rather than held, because every tool
    /// object closes over request-scoped services.</summary>
    IList<AITool> BuildTools();
}
