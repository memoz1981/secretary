namespace Secretary.Domain.Exceptions;

/// <summary>Thrown when a booking is attempted for a blacklisted client — the AI agent (and
/// the web app) must refuse to book for them until the tenant lifts the blacklist.</summary>
public sealed class ClientBlackListedException : DomainException
{
    public ClientBlackListedException(string phoneNumber)
        : base($"Client with phone number '{phoneNumber}' is blacklisted and cannot book appointments.")
    {
    }
}
