using Microsoft.Extensions.AI;
using Secretary.Agents.Tools;
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

    public OrdersAgentModule(
        OrderTools orderTools, EscalationTools escalationTools, CallControlTools callControlTools)
    {
        _orderTools = orderTools;
        _escalationTools = escalationTools;
        _callControlTools = callControlTools;
    }

    public Module Key => Module.Order;

    public string InstructionName => "Order";

    public IList<AITool> BuildTools() =>
    [
        AIFunctionFactory.Create(_orderTools.FindCustomer),
        AIFunctionFactory.Create(_orderTools.FindCustomerByAddress),
        AIFunctionFactory.Create(_orderTools.ConfirmCustomer),
        AIFunctionFactory.Create(_orderTools.RegisterCustomer),
        AIFunctionFactory.Create(_orderTools.GetProductCatalog),
        AIFunctionFactory.Create(_orderTools.AddToOrder),
        AIFunctionFactory.Create(_orderTools.SetOrderQuantity),
        AIFunctionFactory.Create(_orderTools.GetDeliveryDay),
        AIFunctionFactory.Create(_orderTools.PlaceOrder),
        AIFunctionFactory.Create(_escalationTools.EscalateToHuman),
        AIFunctionFactory.Create(_callControlTools.EndCall),
    ];
}
