using Secretary.Application.Dtos;
using FluentValidation;

namespace Secretary.Application.Validation;

public sealed class CreateServiceOfferingRequestValidator : AbstractValidator<CreateServiceOfferingRequest>
{
    public CreateServiceOfferingRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
    }
}

public sealed class UpdateServiceOfferingRequestValidator : AbstractValidator<UpdateServiceOfferingRequest>
{
    public UpdateServiceOfferingRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
    }
}
