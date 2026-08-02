namespace Secretary.Domain.Enums;

/// <summary>Base status shared by every entity (see BaseEntity). Rows are soft-deactivated,
/// never physically deleted, so the audit trail survives.</summary>
public enum EntityStatus
{
    Active,
    Inactive,
}
