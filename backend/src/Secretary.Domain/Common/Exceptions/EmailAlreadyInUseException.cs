namespace Secretary.Domain.Exceptions;

/// <summary>TC-TEAM-06: surfaced as a clean, expected validation error rather than an
/// unhandled unique-constraint DB exception.</summary>
public sealed class EmailAlreadyInUseException : DomainException
{
    public EmailAlreadyInUseException(string email) : base($"'{email}' is already in use by another account.")
    {
    }
}
