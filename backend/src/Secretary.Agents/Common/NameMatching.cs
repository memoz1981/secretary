using System.Globalization;
using System.Text;

namespace Secretary.Agents;

/// <summary>Matches a name the caller said against a name in the database. Voice transcription
/// spells Azerbaijani names inconsistently — Əli/Ali, İlqar/Ilgar, Vüsal/Vusal — which made the
/// agent "find" a provider in one tool and then fail to book with the same person moments later
/// under exact-equality matching. Shared by every tool that takes a spoken name.</summary>
internal static class NameMatching
{
    /// <summary>Matches on a diacritic-stripped, lowercased form, falling back to a unique
    /// substring match ("usta Elvin" → "Elvin"); ambiguous partial matches return null rather
    /// than guessing.</summary>
    public static T? MatchByName<T>(IEnumerable<T> items, Func<T, string> nameOf, string spokenName) where T : class
    {
        var target = Normalize(spokenName);
        if (target.Length == 0)
        {
            return null;
        }

        var exact = items.Where(i => Normalize(nameOf(i)) == target).ToList();
        if (exact.Count == 1)
        {
            return exact[0];
        }

        var partial = items.Where(i =>
        {
            var candidate = Normalize(nameOf(i));
            return candidate.Contains(target) || target.Contains(candidate);
        }).ToList();
        return partial.Count == 1 ? partial[0] : null;
    }

    public static string Normalize(string name)
    {
        // ə (schwa) has no Unicode decomposition, and Turkic dotless ı never folds to plain i,
        // so both need explicit mapping; everything else (ö, ü, ç, ş, ğ, é, …) decomposes under
        // FormD into a base letter + combining mark we can strip.
        var lowered = name.Trim().ToLowerInvariant().Replace('ə', 'e').Replace('ı', 'i');
        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
