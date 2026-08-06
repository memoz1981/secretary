using Secretary.Domain.Enums;

namespace Secretary.Application.Dtos;

/// <summary>Every module, with whether this tenant currently has it — not just the granted ones.
/// The admin screen is a set of switches, and a switch needs to exist before it can be turned
/// on.</summary>
public sealed record TenantModuleResponse(Module Module, bool Enabled);

public sealed record SetTenantModuleRequest(Module Module, bool Enabled);
