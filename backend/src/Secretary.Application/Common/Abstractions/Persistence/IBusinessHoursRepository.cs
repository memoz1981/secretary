using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions.Persistence;

public interface IBusinessHoursRepository : IRepository<BusinessHours>
{
    /// <summary>Every day the tenant has a row for. Fewer than seven means the missing days have
    /// never been set, which the service treats as closed rather than guessing.</summary>
    Task<IReadOnlyList<BusinessHours>> GetWeekAsync(CancellationToken cancellationToken);
}
