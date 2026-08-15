using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One questionnaire — "Servis rəyi", "Satış rəyi" — belonging to a tenant.
///
/// A tenant may have several, because a garage asking about a service visit and asking about a
/// sale are not the same set of questions and their results must not be averaged together. How
/// many they may have is a quota the platform sets; see FeedbackSettings.</summary>
public sealed class Survey : BaseEntity
{
    public int TenantId { get; private set; }
    public string Name { get; private set; }

    private Survey() => Name = string.Empty;

    public static Survey Create(int tenantId, string name, Instant now)
    {
        var survey = new Survey { TenantId = tenantId, Name = Clean(name) };
        survey.InitBase(now);
        return survey;
    }

    public void Rename(string name, Instant now)
    {
        Name = Clean(name);
        Touch(now);
    }

    private static string Clean(string name)
        => string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A questionnaire needs a name.", nameof(name))
            : name.Trim();
}
