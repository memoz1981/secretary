using Secretary.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Secretary.Domain.Enums;

namespace Secretary.Api.Hubs;

/// <summary>Pushes escalation "ringing"/"connected"/"abandoned" state to every connected
/// Owner/Staff client for a tenant (page-inventory.md's global overlay note — any of them
/// can accept). Connections join a per-tenant group on connect so a broadcast never crosses
/// tenants; EscalationsController raises the actual broadcasts after each state change.</summary>
[RequireModule(Module.Appointment)]
[Authorize(Roles = "Owner,Staff")]
public sealed class EscalationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst(AppClaimTypes.TenantId)?.Value;
        if (tenantId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
        }

        await base.OnConnectedAsync();
    }

    public static string TenantGroup(string tenantId) => $"tenant:{tenantId}";
}
