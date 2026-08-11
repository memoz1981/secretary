using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions.Persistence;

/// <summary>The Orders module's repositories. One file because they are one module's worth of
/// small, closely-related contracts, and splitting five of these across five files would add
/// headers and no clarity.</summary>
public interface IMeasurementUnitRepository : IRepository<MeasurementUnit>
{
    Task<MeasurementUnit?> GetByNameAsync(string name, CancellationToken cancellationToken);
}

public interface IProductRepository : IRepository<Product>
{
    /// <summary>The whole catalogue for the tenant, units included — small enough to read whole
    /// and matched against in memory, because a caller says "bidon" and no index helps with
    /// that.</summary>
    Task<IReadOnlyList<Product>> GetCatalogAsync(CancellationToken cancellationToken);
}

public interface ICustomerRepository : IRepository<Customer>
{
    /// <summary>Every customer holding this number. Not one: a household landline can belong to
    /// two people, which is why the phone route confirms rather than identifies.</summary>
    Task<IReadOnlyList<Customer>> FindByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerPhoneNumber>> GetPhoneNumbersAsync(int customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerAddress>> GetAddressesAsync(int customerId, CancellationToken cancellationToken);

    Task AddPhoneNumberAsync(CustomerPhoneNumber phoneNumber, CancellationToken cancellationToken);

    Task AddAddressAsync(CustomerAddress address, CancellationToken cancellationToken);
}

public interface IOrderRepository : IRepository<Order>
{
    Task<IReadOnlyList<Order>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken);
}

public interface IOrderSettingsRepository : IRepository<OrderSettings>
{
    /// <summary>Null when the tenant has never set one — the caller applies the default rather
    /// than a row existing for every tenant whether or not they hold the module.</summary>
    Task<OrderSettings?> GetForCurrentTenantAsync(CancellationToken cancellationToken);
}
