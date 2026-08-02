using Microsoft.AspNetCore.Authorization;
using Secretary.Application.Abstractions;
using Secretary.Domain.Enums;

namespace Secretary.Api.Auth;

/// <summary>Requires the calling tenant to hold a module, on top of whatever roles the endpoint
/// already demands.
///
/// A policy rather than a check inside each action: it guards a whole controller in one line,
/// and a tenant without the module gets 403 rather than a successful response with an empty
/// list. The difference matters — an empty calendar looks like a quiet week, not like a module
/// nobody bought.</summary>
public sealed class RequireModuleAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Module:";

    public RequireModuleAttribute(Module module) => Policy = PolicyPrefix + module;
}

public sealed class ModuleRequirement : IAuthorizationRequirement
{
    public ModuleRequirement(Module module) => Module = module;

    public Module Module { get; }
}

public sealed class ModuleAuthorizationHandler : AuthorizationHandler<ModuleRequirement>
{
    private readonly ICurrentTenantModules _modules;

    public ModuleAuthorizationHandler(ICurrentTenantModules modules) => _modules = modules;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ModuleRequirement requirement)
    {
        // Platform admins hold no modules and are not meant to: their own screens are not
        // module-scoped, and they never reach a module's endpoints. Falling through to the
        // tenant check would deny them correctly anyway, but this says why.
        if (context.User.IsInRole(nameof(AccountRole.PlatformAdmin)))
        {
            return;
        }

        if (await _modules.HasAsync(requirement.Module, CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}

public static class ModuleAuthorizationExtensions
{
    /// <summary>One policy per module, registered up front. Adding a module to the enum makes
    /// its policy exist without another edit here.</summary>
    public static AuthorizationOptions AddModulePolicies(this AuthorizationOptions options)
    {
        foreach (var module in Enum.GetValues<Module>())
        {
            options.AddPolicy(
                RequireModuleAttribute.PolicyPrefix + module,
                policy => policy.AddRequirements(new ModuleRequirement(module)));
        }

        return options;
    }
}
