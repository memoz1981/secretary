using System.Globalization;
using System.Text;

namespace Secretary.Domain.ValueObjects;

/// <summary>Folds a street or building name into the form addresses are compared in.
///
/// Nothing here is shown to anyone. It exists so that "Ə. Cavad küçəsi", "Ahmad Cavad kuc." and
/// "ə.cavad" are one street, which is the difference between recognising a returning customer
/// and creating a second record for them.
///
/// ⚠ Azerbaijani casing is the trap. ToLowerInvariant maps "İ" to "i" plus a combining dot and
/// leaves "I" as "i" rather than "ı", so "İnşaatçılar" quietly stops matching itself. The
/// dotted/dotless pairs and the schwa are folded to ASCII explicitly, which also means a caller
/// spelling a street on a Latin keyboard still matches.</summary>
public static class AddressText
{
    /// <summary>Words that say what kind of road it is rather than which road it is.</summary>
    private static readonly string[] RoadTypes =
        ["küçəsi", "küçə", "kucesi", "kuce", "küç", "kuc", "prospekti", "prospekt", "pr", "yolu"];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var folded = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var lowered = char.ToLower(c, CultureInfo.InvariantCulture);
            folded.Append(lowered switch
            {
                'ə' => 'e',
                'ı' => 'i',
                'İ' => 'i',
                'ç' => 'c',
                'ş' => 's',
                'ğ' => 'g',
                'ö' => 'o',
                'ü' => 'u',
                _ => lowered,
            });
        }

        var words = folded.ToString()
            .Split([' ', '.', ',', '-', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => !RoadTypes.Contains(w, StringComparer.OrdinalIgnoreCase));

        return string.Concat(words);
    }
}
