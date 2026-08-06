using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Abstractions.Persistence;

public interface ICallRepository : IRepository<Call>
{
    Task<IReadOnlyList<Call>> SearchAsync(
        Instant? fromInclusive,
        Instant? toExclusive,
        CallClassification? classification,
        CallOutcome? outcome,
        int? providerId,
        CallPipeline? pipeline,
        CancellationToken cancellationToken);
}
