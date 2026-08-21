using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Application.Pricing;
using Secretary.Domain.Entities;
using Secretary.Domain.Exceptions;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Call Log + Call Detail (Owner/Staff read) and the Agent-role "log this call"
/// write, called once at the end of every call regardless of outcome (functionality-spec.md
/// §3 — "Log every call"). Calls are never deleted (base Status stays Active).</summary>
public sealed class CallService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly TokenPricebook _pricebook;

    /// <summary>Whether this caller may see what a call cost. See CallCostVisibility.</summary>
    private readonly CallCostVisibility _costs;

    public CallService(
        IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant, TokenPricebook pricebook,
        CallCostVisibility costs)
    {
        _costs = costs;
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
        _pricebook = pricebook;
    }

    public async Task<CallResponse> LogAsync(LogCallRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var client = await _uow.Clients.GetByPhoneNumberAsync(request.CallerPhoneNumber, cancellationToken);

        // The one moment this call is ever priced. Everything downstream — Call Log, Call
        // Detail, the KPI dashboard's spend figures — reads the stored number, so correcting a
        // rate in configuration later affects future calls only and leaves history alone.
        //
        // Each model is priced against its own card and only the MONEY is summed. The tokens are
        // summed too, but purely for display: a chained call's recognition, text and synthesis
        // tokens are billed at rates an order of magnitude apart, so their total cannot be
        // priced and is never used for that.
        var usages = request.ModelUsages ?? [];
        var totalUsage = usages.Aggregate(TokenUsage.Zero, (running, entry) => running + entry.Usage);
        var costUsd = _pricebook.CostUsd(usages);

        var call = Call.Log(
            tenantId, client?.Id, request.CallerPhoneNumber, request.RelatedAppointmentId,
            request.Classification, request.Outcome, request.DurationSeconds, request.TurnCount,
            request.CallerTurnCount, request.WaitTimeSeconds, request.RecordingUrl, request.Transcript,
            request.StartedAt, DescribeModels(usages), request.Pipeline, totalUsage, costUsd,
            _clock.GetCurrentInstant());

        await _uow.Calls.AddAsync(call, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(call, client, await _costs.ShowAsync(call.CostUsd, cancellationToken));
    }

    public async Task<IReadOnlyList<CallResponse>> SearchAsync(CallSearchRequest request, CancellationToken cancellationToken)
    {
        var calls = await _uow.Calls.SearchAsync(
            request.From, request.To, request.Classification, request.Outcome, request.ProviderId,
            request.Pipeline, cancellationToken);

        var responses = new List<CallResponse>(calls.Count);
        foreach (var call in calls)
        {
            var client = call.ClientId is int clientId
                ? await _uow.Clients.GetByIdAsync(clientId, cancellationToken)
                : null;
            responses.Add(ToResponse(call, client, await _costs.ShowAsync(call.CostUsd, cancellationToken)));
        }

        return responses;
    }

    public async Task<CallDetailResponse> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var call = await _uow.Calls.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Call), id);

        var client = call.ClientId is int clientId
            ? await _uow.Clients.GetByIdAsync(clientId, cancellationToken)
            : null;

        return new CallDetailResponse(
            ToResponse(call, client, await _costs.ShowAsync(call.CostUsd, cancellationToken)),
            call.Transcript);
    }

    /// <summary>Every model that served the call, for the record's AgentModel column — a chained
    /// call has three and naming only one would make a cost jump impossible to explain later.
    /// Truncated to the column width rather than throwing: losing part of a label is a far
    /// smaller problem than losing the call record it belongs to.</summary>
    private static string DescribeModels(IReadOnlyList<ModelUsage> usages)
    {
        const int agentModelColumnLength = 64;

        var models = usages
            .Select(usage => usage.Model)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var described = string.Join(" + ", models);
        return described.Length <= agentModelColumnLength
            ? described
            : described[..agentModelColumnLength];
    }

    private static CallResponse ToResponse(Call call, Client? client, decimal? costUsd)
        => new(
            call.Id, call.ClientId, call.CallerPhoneNumber, client?.Name, call.RelatedAppointmentId,
            call.Classification, call.Outcome, call.DurationSeconds, call.TurnCount, call.CallerTurnCount,
            call.WaitTimeSeconds, call.RecordingUrl, call.StartedAt, call.AgentModel, call.Pipeline,
            call.TokenUsage, costUsd);
}
