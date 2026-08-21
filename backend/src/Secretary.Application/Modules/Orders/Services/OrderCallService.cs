using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Application.Pricing;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>The order line's call log: the write at the end of every call, and the two reads
/// behind the page.
///
/// A sibling of CallService rather than a branch inside it. The two records share their cost
/// columns and nothing else — this one points at an order and a customer, that one at an
/// appointment and a client — and one service trying to write both would need a nullable half
/// for each module and a flag saying which half is real.</summary>
public sealed class OrderCallService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly TokenPricebook _pricebook;

    /// <summary>Whether this caller may see what a call cost. See CallCostVisibility.</summary>
    private readonly CallCostVisibility _costs;

    public OrderCallService(
        IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant, TokenPricebook pricebook,
        CallCostVisibility costs)
    {
        _costs = costs;
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
        _pricebook = pricebook;
    }

    public async Task<OrderCallResponse> LogAsync(LogOrderCallRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        // The one moment this call is ever priced. Everything downstream reads the stored
        // number, so correcting a rate later affects future calls only and leaves history alone.
        var usages = request.ModelUsages ?? [];
        var totalUsage = usages.Aggregate(TokenUsage.Zero, (running, entry) => running + entry.Usage);
        var costUsd = _pricebook.CostUsd(usages);

        var call = OrderCall.Log(
            tenantId, request.CustomerId, request.CallerPhoneNumber, request.RelatedOrderId, request.Outcome,
            request.DurationSeconds, request.TurnCount, request.CallerTurnCount, request.WaitTimeSeconds,
            request.RecordingUrl, request.Transcript, request.StartedAt, DescribeModels(usages), request.Pipeline,
            totalUsage, costUsd, _clock.GetCurrentInstant());

        await _uow.OrderCalls.AddAsync(call, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(call, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderCallResponse>> SearchAsync(
        Instant? from, Instant? to, CallOutcome? outcome, CancellationToken cancellationToken)
    {
        var calls = await _uow.OrderCalls.SearchAsync(from, to, outcome, cancellationToken);
        if (calls.Count == 0)
        {
            return [];
        }

        // One read of the customers rather than one per call — a phone-order business has a lot
        // of short calls, and the page shows a month of them.
        var names = (await _uow.Customers.GetAllAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

        var showCosts = await _costs.IsAllowedAsync(cancellationToken);
        return calls
            .Select(call => ToResponse(
                call, NameFor(names, call.CustomerId), showCosts ? call.CostUsd : null))
            .ToList();
    }

    public async Task<OrderCallDetailResponse> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var call = await _uow.OrderCalls.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(OrderCall), id);

        return new OrderCallDetailResponse(await ToResponseAsync(call, cancellationToken), call.Transcript);
    }

    private async Task<OrderCallResponse> ToResponseAsync(OrderCall call, CancellationToken cancellationToken)
    {
        var customer = call.CustomerId is int customerId
            ? await _uow.Customers.GetByIdAsync(customerId, cancellationToken)
            : null;

        return ToResponse(call, customer?.Name, await _costs.ShowAsync(call.CostUsd, cancellationToken));
    }

    private static string? NameFor(IReadOnlyDictionary<int, string?> names, int? customerId)
        => customerId is int id && names.TryGetValue(id, out var name) ? name : null;

    /// <summary>Every model that served the call. One on the realtime path; naming only one of
    /// several would make a jump in the cost chart impossible to explain later. Truncated to the
    /// column width rather than throwing — losing part of a label is a far smaller problem than
    /// losing the call record it belongs to.</summary>
    private static string DescribeModels(IReadOnlyList<ModelUsage> usages)
    {
        const int agentModelColumnLength = 64;

        var models = usages
            .Select(usage => usage.Model)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var described = string.Join(" + ", models);
        return described.Length <= agentModelColumnLength ? described : described[..agentModelColumnLength];
    }

    private static OrderCallResponse ToResponse(OrderCall call, string? customerName, decimal? costUsd)
        => new(
            call.Id, call.CustomerId, customerName, call.CallerPhoneNumber, call.RelatedOrderId, call.Outcome,
            call.DurationSeconds, call.TurnCount, call.CallerTurnCount, call.StartedAt,
            costUsd is null ? null : call.AgentModel,
            call.Pipeline, call.TokenUsage, costUsd);
}
