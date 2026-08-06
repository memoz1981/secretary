using Secretary.Application.Dtos;
using FluentValidation;

namespace Secretary.Application.Validation;

public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).MaximumLength(200);
    }
}

public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).MaximumLength(200);
    }
}
