using System.ComponentModel;
using Secretary.Application.Dtos;
using Secretary.Application.Services;

namespace Secretary.Agents.Tools;

public sealed class EscalationTools
{
    private readonly EscalationService _escalationService;

    public EscalationTools(EscalationService escalationService) => _escalationService = escalationService;

    [Description("Transfers the call to a human staff member. Tell the caller you're transferring them and ask " +
                 "them to hold BEFORE calling this. Use when you can't resolve the request, the caller asks for " +
                 "a human, or there's a complaint.")]
    public async Task<string> EscalateToHuman(
        [Description("The caller's phone number")] string callerPhoneNumber,
        [Description("A short, specific reason staff can act on immediately without re-asking the caller everything")] string reason)
    {
        await _escalationService.RaiseAsync(new RaiseEscalationRequest(callerPhoneNumber, reason), default);
        return "Transfer started.";
    }
}
