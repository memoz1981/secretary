namespace Secretary.Domain.Exceptions;

/// <summary>Thrown when availability or a booking is requested for a provider that doesn't
/// actively offer the requested service (see ProviderServiceOffering).</summary>
public sealed class ProviderDoesNotOfferServiceException : DomainException
{
    public ProviderDoesNotOfferServiceException(int providerId, int serviceOfferingId)
        : base($"Provider '{providerId}' does not offer service '{serviceOfferingId}'.")
    {
    }
}
