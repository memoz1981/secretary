using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>The Products page: what the tenant sells, and the words their callers use for it.
///
/// Aliases are the field that will decide whether the agent works. A caller asks for "bidon" or
/// "balon" and the catalogue says "Sirab 20 litrlik bidon" — without the alias the agent tells a
/// paying customer the business does not sell what it sells.</summary>
public sealed class ProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;

    public ProductService(IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<UnitResponse>> ListUnitsAsync(CancellationToken cancellationToken)
    {
        var units = await _uow.Units.GetAllAsync(cancellationToken);
        return units.Select(u => new UnitResponse(u.Id, u.Name)).ToList();
    }

    public async Task<IReadOnlyList<ProductDetailResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var products = await _uow.Products.GetCatalogAsync(cancellationToken);
        var unitNames = await UnitNamesAsync(cancellationToken);
        return products.Select(p => ToResponse(p, unitNames)).ToList();
    }

    public async Task<ProductDetailResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var product = Product.Create(
            tenantId, request.Name, request.MeasurementUnitId, request.UnitPrice, request.Aliases,
            request.MaxOrderQuantity, _clock.GetCurrentInstant());

        await _uow.Products.AddAsync(product, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(product, await UnitNamesAsync(cancellationToken));
    }

    public async Task<ProductDetailResponse> UpdateAsync(
        int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _uow.Products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);

        product.UpdateDetails(
            request.Name, request.MeasurementUnitId, request.UnitPrice, request.Aliases, request.MaxOrderQuantity,
            _clock.GetCurrentInstant());

        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(product, await UnitNamesAsync(cancellationToken));
    }

    /// <summary>Soft-deactivates. Past order lines keep pointing at the row, and a product that
    /// comes back next season should come back as itself.</summary>
    public async Task RemoveAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _uow.Products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);

        product.Deactivate(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<int, string>> UnitNamesAsync(CancellationToken cancellationToken)
    {
        var units = await _uow.Units.GetAllAsync(cancellationToken);
        return units.ToDictionary(u => u.Id, u => u.Name);
    }

    private static ProductDetailResponse ToResponse(Product p, IReadOnlyDictionary<int, string> unitNames)
        => new(p.Id, p.Name, p.MeasurementUnitId,
            unitNames.TryGetValue(p.MeasurementUnitId, out var unit) ? unit : string.Empty, p.UnitPrice, p.Aliases,
            p.MaxOrderQuantity);
}
