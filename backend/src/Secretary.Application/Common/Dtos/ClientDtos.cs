using NodaTime;

namespace Secretary.Application.Dtos;

public sealed record ClientResponse(
    int Id, string PhoneNumber, string? Name, bool BlackListed, string? BlackListReason, Instant CreatedAt);

public sealed record CreateClientRequest(string PhoneNumber, string? Name);

public sealed record UpdateClientRequest(string PhoneNumber, string? Name);

public sealed record BlackListClientRequest(string? Reason);
