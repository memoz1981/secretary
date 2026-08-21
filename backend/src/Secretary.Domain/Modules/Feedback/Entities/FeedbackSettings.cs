using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>What this tenant is allowed, set by the platform rather than by them.
///
/// The quota is the only thing here so far. It exists because a questionnaire is the unit the
/// product is sold in — one tenant buys three, another buys ten — so the limit has to be a
/// number the platform sets and the tenant cannot move, enforced where surveys are created and
/// not merely greyed out in the UI.
///
/// Same shape as OrderSettings: a row per tenant, created on first save rather than seeded for
/// everyone, with the default below applying until then.</summary>
public sealed class FeedbackSettings : BaseEntity
{
    /// <summary>Enough for a garage to separate service from sales and still have one spare.</summary>
    public const int DefaultMaxSurveys = 3;

    public int TenantId { get; private set; }
    public int MaxSurveys { get; private set; }

    private FeedbackSettings()
    {
    }

    public static FeedbackSettings Create(int tenantId, int maxSurveys, Instant now)
    {
        var settings = new FeedbackSettings { TenantId = tenantId, MaxSurveys = Validate(maxSurveys) };
        settings.InitBase(now);
        return settings;
    }

    public void Update(int maxSurveys, Instant now)
    {
        MaxSurveys = Validate(maxSurveys);
        Touch(now);
    }

    /// <summary>Zero is allowed and means "this tenant holds the module but may not build one
    /// yet" — a real state during onboarding, and different from having no row at all.</summary>
    private static int Validate(int maxSurveys)
        => maxSurveys is >= 0 and <= 100
            ? maxSurveys
            : throw new ArgumentOutOfRangeException(
                nameof(maxSurveys), maxSurveys, "A questionnaire quota must be between 0 and 100.");
}
