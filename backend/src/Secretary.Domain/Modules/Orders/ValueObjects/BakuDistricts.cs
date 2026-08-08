using System.Globalization;

namespace Secretary.Domain.ValueObjects;

/// <summary>The twelve rayons of Baku, and the one job that needs them: turning what a caller
/// said into one canonical spelling.
///
/// A static list rather than a table or an enum. A table would need seeding, migrating and an
/// admin screen for twelve values that have not changed in decades; an enum would need ASCII
/// identifiers and a separate display name anyway, because "Binəqədi" as a C# identifier is
/// legal and a bad idea.
///
/// ⚠ Baku only. Every tenant is a Baku business today, the same assumption already baked into
/// the hardcoded Asia/Baku timezone. A tenant in Gəncə needs this replaced, not extended.</summary>
public static class BakuDistricts
{
    public static readonly IReadOnlyList<string> All =
    [
        "Binəqədi", "Qaradağ", "Xətai", "Xəzər", "Nərimanov", "Nəsimi",
        "Nizami", "Pirallahı", "Sabunçu", "Səbail", "Suraxanı", "Yasamal",
    ];

    /// <summary>The canonical spelling of what the caller said, or null if it is not a Baku
    /// rayon. Callers say "Nəsimi rayonu", "nesimi", "Nesimi r." — all the same place.</summary>
    public static string? Match(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var needle = Fold(spoken);
        return All.FirstOrDefault(d => Fold(d) == needle);
    }

    public static bool IsKnown(string? spoken) => Match(spoken) is not null;

    /// <summary>Casefold for comparison only — never for storage or display.
    ///
    /// Azerbaijani casing is the trap here: ToLowerInvariant turns "İ" into "i̇" (i plus a
    /// combining dot) and leaves "I" as "i" rather than "ı", so "İnşaatçılar" stops matching
    /// itself. The schwa and the dotted/dotless i pairs are folded explicitly, which also makes
    /// "nesimi" match "Nəsimi" — a caller spelling it on a Latin keyboard means the same
    /// rayon.</summary>
    private static string Fold(string value)
    {
        var folded = value
            .Replace("rayonu", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("rayon", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim()
            .Trim('.', ',', 'r', 'R')
            .Trim();

        var builder = new System.Text.StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            var lowered = char.ToLower(c, CultureInfo.InvariantCulture);
            builder.Append(lowered switch
            {
                'ə' or 'İ' => lowered == 'ə' ? 'e' : 'i',
                'ı' => 'i',
                'ç' => 'c',
                'ş' => 's',
                'ğ' => 'g',
                'ö' => 'o',
                'ü' => 'u',
                _ => lowered,
            });
        }

        return builder.ToString().Replace(" ", string.Empty);
    }
}
