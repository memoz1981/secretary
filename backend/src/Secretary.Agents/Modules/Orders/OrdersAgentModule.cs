using Microsoft.Extensions.AI;
using Secretary.Agents.Orders;
using Secretary.Agents.Tools;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Domain.Enums;

namespace Secretary.Agents;

/// <summary>The order line. A different agent from the appointment one in every way that
/// matters: its own toolset, its own instruction file, its own phone number.
///
/// EscalateToHuman and EndCall come along because they are not appointment tools — one is how
/// any call reaches a person, the other is how any call ends.</summary>
public sealed class OrdersAgentModule : IAgentModule
{
    private readonly OrderTools _orderTools;
    private readonly EscalationTools _escalationTools;
    private readonly CallControlTools _callControlTools;
    private readonly OrderCallSession _session;
    private readonly OrderCallService _calls;

    public OrdersAgentModule(
        OrderTools orderTools, EscalationTools escalationTools, CallControlTools callControlTools,
        OrderCallSession session, OrderCallService calls)
    {
        _orderTools = orderTools;
        _escalationTools = escalationTools;
        _callControlTools = callControlTools;
        _session = session;
        _calls = calls;
    }

    public Module Key => Module.Order;

    public string InstructionName => "Order";

    public IList<AITool> BuildTools() =>
    [
        AIFunctionFactory.Create(_orderTools.FindCustomerById),
        AIFunctionFactory.Create(_orderTools.FindCustomerByPhone),
        AIFunctionFactory.Create(_orderTools.FindCustomerByAddress),
        AIFunctionFactory.Create(_orderTools.RegisterCustomer),
        AIFunctionFactory.Create(_orderTools.PlaceOrder),
        AIFunctionFactory.Create(_orderTools.CancelOrder),
        AIFunctionFactory.Create(_escalationTools.EscalateToHuman),
        AIFunctionFactory.Create(_callControlTools.EndCall),
    ];

    /// <summary>ord.Calls, with the two things only this module's tools could know: who was
    /// identified, and which order came out of it. Both come from the per-call session rather
    /// than from a phone number — a browser call has none, and even a real one may belong to a
    /// customer whose record lists a different number.
    ///
    /// The entry's Classification is ignored on purpose; see CallLogEntry.</summary>
    public async Task LogCallAsync(CallLogEntry entry, CancellationToken cancellationToken)
        => await _calls.LogAsync(
            new LogOrderCallRequest(
                _session.CustomerId, entry.CallerPhoneNumber, _session.PlacedOrderId, entry.Outcome,
                entry.DurationSeconds, entry.TurnCount, entry.CallerTurnCount, WaitTimeSeconds: null,
                entry.RecordingUrl, entry.Transcript, entry.StartedAt, entry.Pipeline, entry.ModelUsages),
            cancellationToken);
}
