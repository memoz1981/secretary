using Secretary.Application.Dtos;
using FluentValidation;

namespace Secretary.Application.Validation;

public sealed class RaiseEscalationRequestValidator : AbstractValidator<RaiseEscalationRequest>
{
    public RaiseEscalationRequestValidator()
    {
        RuleFor(x => x.CallerPhoneNumber).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}
