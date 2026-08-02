using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Domain.Abstractions;

/// <summary>Common shape of every persisted entity: int identity PK, audit timestamps and an
/// Active/Inactive status. Id is 0 until EF Core saves the row (SQL Server IDENTITY(1,1)).</summary>
public abstract class BaseEntity
{
    public int Id { get; protected set; }
    public Instant CreatedAt { get; protected set; }
    public Instant UpdatedAt { get; protected set; }
    public EntityStatus Status { get; protected set; }

    protected void InitBase(Instant now)
    {
        CreatedAt = now;
        UpdatedAt = now;
        Status = EntityStatus.Active;
    }

    protected void Touch(Instant now) => UpdatedAt = now;

    public void Deactivate(Instant now)
    {
        Status = EntityStatus.Inactive;
        UpdatedAt = now;
    }

    public void Reactivate(Instant now)
    {
        Status = EntityStatus.Active;
        UpdatedAt = now;
    }
}
