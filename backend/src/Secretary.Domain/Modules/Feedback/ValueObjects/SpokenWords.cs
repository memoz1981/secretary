using System.Globalization;
using System.Text;

namespace Secretary.Domain.ValueObjects;

/// <summary>What the caller said, folded to ASCII and split into words.
///
/// ⚠ Deliberately not <see cref="AddressText"/>, which folds the same letters and then throws the
/// spaces away — it compares whole street names, so a single run of characters is what it wants.
/// Here the words matter separately: "hə" is yes, and looking for "he" inside a run like
/// "hersey" would match the middle of a word that means nothing of the kind.
///
/// So the folding is shared in spirit and not in code, and the difference is the point.</summary>
public static class SpokenWords
{
    private static readonly char[] Separators = [' ', '.', ',', '!', '?', ';', ':', '-', '\n', '\r', '\t'];

    /// <summary>Lowercased, Azerbaijani letters folded to ASCII, punctuation dropped.
    ///
    /// Same fold as addresses use, and for the same reason: ToLowerInvariant turns "İ" into "i"
    /// plus a combining dot and leaves "I" as "i" rather than "ı", so a word stops matching
    /// itself. And a caller's words arrive from a transcriber that may or may not use the
    /// Azerbaijani letters at all.</summary>
    public static IReadOnlyList<string> Fold(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return [];
        }

        var folded = new StringBuilder(spoken.Length);
        foreach (var c in spoken)
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

        return folded.ToString().Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

/// <summary>Yes and no, in all the ways people actually say them.
///
/// ⚠ A real caller answered "hə" to a Bəli/Xeyr question and was refused, twice, and the call
/// ended. Nobody says "bəli" out loud — it is the written word, and the stored option label — so
/// matching the label alone means the one question type that should never fail is the one that
/// fails most.
///
/// Russian and English are here because the agent switches language when the caller does, and a
/// caller who switched will answer in the language they switched to.</summary>
public static class YesNoWords
{
    /// <summary>Folded, so "hə" is "he" and "əlbəttə" is "elbette".</summary>
    private static readonly HashSet<string> Yes = new(StringComparer.Ordinal)
    {
        "beli", "he", "hee", "ha", "hehe", "elbette", "tebii", "tebiiki", "razi", "raziyam",
        "oldu", "duzdur", "tesdiq", "tesdiqleyirem", "da", "yes", "yeah", "yep", "ok", "okey",
    };

    private static readonly HashSet<string> No = new(StringComparer.Ordinal)
    {
        "xeyr", "xeyir", "yox", "yoxdur", "yoxe", "deyil", "net", "no", "nope", "nah",
    };

    /// <summary>True for yes, false for no, null for neither — and null for both.
    ///
    /// Both matters: "hə, yox, gözləmədim" contains one of each, and picking whichever the loop
    /// reached first would record an answer on a coin toss. Refusing sends it back for one repeat,
    /// which is what an ambiguous answer deserves.</summary>
    public static bool? Read(string? spoken)
    {
        var words = SpokenWords.Fold(spoken);
        var saidYes = words.Any(Yes.Contains);
        var saidNo = words.Any(No.Contains);

        return saidYes == saidNo ? null : saidYes;
    }
}
