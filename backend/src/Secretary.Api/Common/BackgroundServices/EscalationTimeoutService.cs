using Secretary.Api.Hubs;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Microsoft.AspNetCore.SignalR;
using NodaTime;

namespace Secretary.Api.BackgroundServices;

/// <summary>Flow D: "no staff accepts within a timeout" → Escalated → abandoned. Runs as a
/// system job across every tenant at once (see IEscalationRepository's cross-tenant method),
/// since there's no single ambient tenant for a background sweep — each match is abandoned
/// and broadcast individually, using that escalation's own tenant for the SignalR group.</summary>
public sealed class EscalationTimeoutService : BackgroundService
{
    private static readonly Duration RingingTimeout = Duration.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<EscalationHub> _hub;
    private readonly ILogger<EscalationTimeoutService> _logger;

    public EscalationTimeoutService(IServiceScopeFactory scopeFactory, IHubContext<EscalationHub> hub, ILogger<EscalationTimeoutService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Escalation timeout sweep failed.");
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var cutoff = clock.GetCurrentInstant() - RingingTimeout;
        var timedOut = await uow.Escalations.GetRingingOlderThanAcrossAllTenantsAsync(cutoff, cancellationToken);
        if (timedOut.Count == 0)
        {
            return;
        }

        var now = clock.GetCurrentInstant();
        foreach (var escalation in timedOut)
        {
            escalation.Abandon(now);
        }

        await uow.SaveChangesAsync(cancellationToken);

        foreach (var escalation in timedOut)
        {
            // Cross-tenant sweep has no ambient tenant, so the tenant-filtered lookup
            // wouldn't find the client — use the explicit cross-tenant read.
            var client = await uow.Clients.GetByIdAcrossAllTenantsAsync(escalation.ClientId, cancellationToken);

            var response = new EscalationResponse(
                escalation.Id, escalation.ClientId, client?.PhoneNumber ?? string.Empty, client?.Name,
                escalation.Reason, escalation.EscalationStatus, escalation.RaisedAt,
                escalation.AcceptedByAccountId, escalation.AcceptedAt);

            await _hub.Clients.Group(EscalationHub.TenantGroup(escalation.TenantId.ToString()))
                .SendAsync("escalationAbandoned", response, cancellationToken);
        }
    }
}
