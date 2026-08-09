using Secretary.Api.Auth;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;

namespace Secretary.Api.Controllers;

/// <summary>Products page — Owner read/write, Staff read-only, the same split the Services page
/// uses.</summary>
[ApiController]
[RequireModule(Module.Order)]
[Authorize(Roles = "Owner,Staff")]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductService _products;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<UpdateProductRequest> _updateValidator;

    public ProductsController(
        ProductService products,
        IValidator<CreateProductRequest> createValidator,
        IValidator<UpdateProductRequest> updateValidator)
    {
        _products = products;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDetailResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _products.ListAsync(cancellationToken));

    /// <summary>The three seeded units. A dropdown, not free text — "kq" and "kg" as two units
    /// would split a catalogue in half.</summary>
    [HttpGet("units")]
    public async Task<ActionResult<IReadOnlyList<UnitResponse>>> Units(CancellationToken cancellationToken)
        => Ok(await _products.ListUnitsAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProductDetailResponse>> Create(
        CreateProductRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _products.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProductDetailResponse>> Update(
        [FromRoute] int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _products.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        await _products.RemoveAsync(id, cancellationToken);
        return NoContent();
    }
}
