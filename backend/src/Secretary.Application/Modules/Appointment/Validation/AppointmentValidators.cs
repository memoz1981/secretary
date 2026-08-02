using Secretary.Application.Dtos;
using FluentValidation;

namespace Secretary.Application.Validation;

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.ClientPhoneNumber).NotEmpty();
        RuleFor(x => x).Must(x => x.End > x.Start)
            .WithMessage("End must be after Start.");
    }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x).Must(x => x.End > x.Start)
            .WithMessage("End must be after Start.");
    }
}

public sealed class AvailabilityRequestValidator : AbstractValidator<AvailabilityRequest>
{
    public AvailabilityRequestValidator()
    {
        RuleFor(x => x).Must(x => x.To > x.From)
            .WithMessage("To must be after From.");
    }
}
