using Secretary.Application.Dtos;
using FluentValidation;

namespace Secretary.Application.Validation;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);

        // Free text, comma separated: the words callers actually use. Length-capped because it
        // is read whole on every catalogue load, not because anyone needs five hundred synonyms.
        RuleFor(r => r.Aliases).MaximumLength(500);
        RuleFor(r => r.MeasurementUnitId).GreaterThan(0);
        RuleFor(r => r.UnitPrice).GreaterThanOrEqualTo(0);

        // Empty means no limit; zero would make the product unorderable, which is what
        // deactivating it is for.
        RuleFor(r => r.MaxOrderQuantity).GreaterThan(0).When(r => r.MaxOrderQuantity.HasValue);
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Aliases).MaximumLength(500);
        RuleFor(r => r.MeasurementUnitId).GreaterThan(0);
        RuleFor(r => r.UnitPrice).GreaterThanOrEqualTo(0);

        // Empty means no limit; zero would make the product unorderable, which is what
        // deactivating it is for.
        RuleFor(r => r.MaxOrderQuantity).GreaterThan(0).When(r => r.MaxOrderQuantity.HasValue);
    }
}
