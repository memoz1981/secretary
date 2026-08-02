using Secretary.Application.Dtos;

namespace Secretary.Agents.ServiceCatalog;

/// <summary>Who performs a given service, cached per tenant the same way the service catalog
/// is (see <see cref="ITenantServiceCatalogCache"/>). Every booking flow asks this at least
/// once — naming the eligible providers, checking availability, and again when the booking is
/// made — and the answer only changes when the Owner edits the Providers page.
///
/// Availability and appointments are deliberately NOT cached anywhere: a slot that was free a
/// minute ago may not be now, and handing a caller a stale one means a double booking.</summary>
public interface ITenantProviderDirectory
{
    Task<IReadOnlyList<ProviderResponse>> GetProvidersForServiceAsync(int serviceOfferingId, CancellationToken cancellationToken);
}
