using NodaTime;

namespace Secretary.Application.Dtos;

public sealed record ServiceOfferingResponse(
    int Id, string Name, decimal Price, int DurationMinutes, Instant UpdatedAt);

public sealed record CreateServiceOfferingRequest(string Name, decimal Price, int DurationMinutes);

public sealed record UpdateServiceOfferingRequest(string Name, decimal Price, int DurationMinutes);
