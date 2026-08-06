using Secretary.Application.Dtos;
using FluentValidation;

namespace Secretary.Application.Validation;

public sealed class LogCallRequestValidator : AbstractValidator<LogCallRequest>
{
    public LogCallRequestValidator()
    {
        RuleFor(x => x.CallerPhoneNumber).NotEmpty();
        RuleFor(x => x.DurationSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TurnCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CallerTurnCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RecordingUrl).NotEmpty();

        // A negative token count would price a call below zero and pull the whole range's spend
        // figure down with it, so it is rejected at the edge rather than clamped out of sight.
        When(x => x.ModelUsages is not null, () =>
        {
            RuleForEach(x => x.ModelUsages!).ChildRules(entry =>
            {
                entry.RuleFor(x => x.Model).NotEmpty();
                entry.RuleFor(x => x.Usage.InputTextTokens).GreaterThanOrEqualTo(0);
                entry.RuleFor(x => x.Usage.CachedInputTextTokens).GreaterThanOrEqualTo(0);
                entry.RuleFor(x => x.Usage.InputAudioTokens).GreaterThanOrEqualTo(0);
                entry.RuleFor(x => x.Usage.CachedInputAudioTokens).GreaterThanOrEqualTo(0);
                entry.RuleFor(x => x.Usage.OutputTextTokens).GreaterThanOrEqualTo(0);
                entry.RuleFor(x => x.Usage.OutputAudioTokens).GreaterThanOrEqualTo(0);
            });
        });
    }
}
