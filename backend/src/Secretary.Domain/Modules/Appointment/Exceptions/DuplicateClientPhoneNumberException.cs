namespace Secretary.Domain.Exceptions;

/// <summary>Clients are keyed by phone number within a tenant (the AI agent's lookup key),
/// so two client records with the same number would be ambiguous.</summary>
public sealed class DuplicateClientPhoneNumberException : DomainException
{
    public DuplicateClientPhoneNumberException(string phoneNumber)
        : base($"A client with phone number '{phoneNumber}' already exists.")
    {
    }
}
