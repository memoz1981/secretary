using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class MeasurementUnitRepository : IMeasurementUnitRepository
{
    private readonly AppDbContext _db;

    public MeasurementUnitRepository(AppDbContext db) => _db = db;

    public async Task<MeasurementUnit?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MeasurementUnit>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Units.OrderBy(u => u.Id).ToListAsync(cancellationToken);

    public async Task AddAsync(MeasurementUnit entity, CancellationToken cancellationToken)
        => await _db.Units.AddAsync(entity, cancellationToken);

    public void Update(MeasurementUnit entity) => _db.Units.Update(entity);

    public void Remove(MeasurementUnit entity) => _db.Units.Remove(entity);

    public async Task<MeasurementUnit?> GetByNameAsync(string name, CancellationToken cancellationToken)
        => await _db.Units.FirstOrDefaultAsync(u => u.Name == name, cancellationToken);
}

internal sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Products.OrderBy(p => p.Name).ToListAsync(cancellationToken);

    public async Task AddAsync(Product entity, CancellationToken cancellationToken)
        => await _db.Products.AddAsync(entity, cancellationToken);

    public void Update(Product entity) => _db.Products.Update(entity);

    public void Remove(Product entity) => _db.Products.Remove(entity);

    public async Task<IReadOnlyList<Product>> GetCatalogAsync(CancellationToken cancellationToken)
        => await _db.Products
            .Where(p => p.Status == EntityStatus.Active)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
}

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;

    public CustomerRepository(AppDbContext db) => _db = db;

    public async Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Customers.OrderBy(c => c.Id).ToListAsync(cancellationToken);

    public async Task AddAsync(Customer entity, CancellationToken cancellationToken)
        => await _db.Customers.AddAsync(entity, cancellationToken);

    public void Update(Customer entity) => _db.Customers.Update(entity);

    public void Remove(Customer entity) => _db.Customers.Remove(entity);

    /// <summary>Joined through the numbers table so the tenant filter on Customers still
    /// applies — the numbers themselves carry no tenant of their own.</summary>
    public async Task<IReadOnlyList<Customer>> FindByPhoneNumberAsync(
        string phoneNumber, CancellationToken cancellationToken)
        => await (from number in _db.CustomerPhoneNumbers
                  join customer in _db.Customers on number.CustomerId equals customer.Id
                  where number.PhoneNumber == phoneNumber && number.Status == EntityStatus.Active
                  select customer)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <summary>Joined the same way and for the same reason as the phone lookup: the addresses
    /// table carries no tenant of its own, so the filter has to arrive through Customers.</summary>
    public async Task<IReadOnlyList<Customer>> FindByAddressAsync(
        string district, string normalizedStreet, CancellationToken cancellationToken)
        => await (from address in _db.CustomerAddresses
                  join customer in _db.Customers on address.CustomerId equals customer.Id
                  where address.Status == EntityStatus.Active
                        && address.District == district
                        && address.StreetNormalized == normalizedStreet
                  select customer)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerPhoneNumber>> GetPhoneNumbersAsync(
        int customerId, CancellationToken cancellationToken)
        => await _db.CustomerPhoneNumbers
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.IsPrimary)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerAddress>> GetAddressesAsync(
        int customerId, CancellationToken cancellationToken)
        => await _db.CustomerAddresses
            .Where(a => a.CustomerId == customerId && a.Status == EntityStatus.Active)
            .OrderByDescending(a => a.IsDefault)
            .ToListAsync(cancellationToken);

    public async Task AddPhoneNumberAsync(CustomerPhoneNumber phoneNumber, CancellationToken cancellationToken)
        => await _db.CustomerPhoneNumbers.AddAsync(phoneNumber, cancellationToken);

    public async Task AddAddressAsync(CustomerAddress address, CancellationToken cancellationToken)
        => await _db.CustomerAddresses.AddAsync(address, cancellationToken);
}

internal sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db) => _db = db;

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Orders.OrderByDescending(o => o.PlacedAt).ToListAsync(cancellationToken);

    public async Task AddAsync(Order entity, CancellationToken cancellationToken)
        => await _db.Orders.AddAsync(entity, cancellationToken);

    public void Update(Order entity) => _db.Orders.Update(entity);

    public void Remove(Order entity) => _db.Orders.Remove(entity);

    public async Task<IReadOnlyList<Order>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken)
        => await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAt)
            .ToListAsync(cancellationToken);
}

internal sealed class OrderSettingsRepository : IOrderSettingsRepository
{
    private readonly AppDbContext _db;

    public OrderSettingsRepository(AppDbContext db) => _db = db;

    public async Task<OrderSettings?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.OrderSettings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OrderSettings>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.OrderSettings.ToListAsync(cancellationToken);

    public async Task AddAsync(OrderSettings entity, CancellationToken cancellationToken)
        => await _db.OrderSettings.AddAsync(entity, cancellationToken);

    public void Update(OrderSettings entity) => _db.OrderSettings.Update(entity);

    public void Remove(OrderSettings entity) => _db.OrderSettings.Remove(entity);

    public async Task<OrderSettings?> GetForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.OrderSettings.FirstOrDefaultAsync(cancellationToken);
}
