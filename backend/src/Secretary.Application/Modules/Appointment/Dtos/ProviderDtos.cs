namespace Secretary.Application.Dtos;

public sealed record ProviderResponse(int Id, string Name);

public sealed record CreateProviderRequest(string Name);

public sealed record UpdateProviderRequest(string Name);

/// <summary>One cell of the Providers page checkbox matrix — Active means the provider
/// performs that service offering.</summary>
public sealed record ProviderServiceAssignmentResponse(int ProviderId, int ServiceOfferingId, bool Active);

public sealed record ProviderServiceMatrixResponse(
    IReadOnlyList<ProviderResponse> Providers,
    IReadOnlyList<ServiceOfferingResponse> ServiceOfferings,
    IReadOnlyList<ProviderServiceAssignmentResponse> Assignments);

public sealed record SetProviderServiceAssignmentRequest(int ServiceOfferingId, bool Active);
