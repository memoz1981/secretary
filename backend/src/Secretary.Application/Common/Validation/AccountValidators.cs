using Secretary.Application.Dtos;
using FluentValidation;

namespace Secretary.Application.Validation;

public sealed class AddStaffRequestValidator : AbstractValidator<AddStaffRequest>
{
    public AddStaffRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class ResetAccountPasswordRequestValidator : AbstractValidator<ResetAccountPasswordRequest>
{
    public ResetAccountPasswordRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
