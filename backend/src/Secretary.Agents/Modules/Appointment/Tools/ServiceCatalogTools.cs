using System.ComponentModel;
using Secretary.Agents.ServiceCatalog;

namespace Secretary.Agents.Tools;

public sealed class ServiceCatalogTools
{
    private readonly ITenantServiceCatalogCache _catalog;

    public ServiceCatalogTools(ITenantServiceCatalogCache catalog) => _catalog = catalog;

    /// <summary>Names only. Prices and durations used to come back with every listing, and the
    /// model recited them at a caller who had only asked what the business does — data in
    /// context gets spoken. GetServiceDetails supplies the numbers when they're actually asked
    /// for.</summary>
    [Description("The names of the services this business offers. Use GetServiceDetails when the caller asks " +
                 "about price or duration.")]
    public async Task<string> GetServiceCatalog()
    {
        var services = await _catalog.GetCatalogAsync(default);
        return services.Count == 0
            ? "No services listed yet."
            : string.Join(", ", services.Select(s => s.Name));
    }

    [Description("Price and approximate duration of one named service. Never state a price or duration that " +
                 "did not come from this tool.")]
    public async Task<string> GetServiceDetails(
        [Description("Exact service name, matching GetServiceCatalog")] string serviceName)
    {
        var services = await _catalog.GetCatalogAsync(default);
        var service = NameMatching.MatchByName(services, s => s.Name, serviceName);
        return service is null
            ? $"No such service. Services: {string.Join(", ", services.Select(s => s.Name))}."
            : $"{service.Name}: {service.Price} AZN, {service.DurationMinutes} min.";
    }
}
