namespace Secretary.Domain.ValueObjects;

/// <summary>Reads the number out of a döngə.
///
/// Baku's private-house areas are numbered lanes off a main street, and a caller says the same
/// lane three ways: "beşinci döngə", "5-ci döngə", "5 döngə". Stored as spoken, compared as a
/// number — because house 12 exists on every lane of a street, so an address match that ignores
/// the lane matches the wrong customer in exactly the areas where a wrong delivery is hardest
/// to undo.
///
/// Returns null for a named lane, which does exist; those fall back to text comparison.</summary>
public static class LaneNumber
{
    /// <summary>Azerbaijani ordinals as far as anyone numbers a lane. Beyond this the digits are
    /// said as digits.</summary>
    private static readonly Dictionary<string, int> SpelledOut = new(StringComparer.OrdinalIgnoreCase)
    {
        ["birinci"] = 1, ["ikinci"] = 2, ["üçüncü"] = 3, ["ucuncu"] = 3, ["dördüncü"] = 4, ["dorduncu"] = 4,
        ["beşinci"] = 5, ["besinci"] = 5, ["altıncı"] = 6, ["altinci"] = 6, ["yeddinci"] = 7,
        ["səkkizinci"] = 8, ["sekkizinci"] = 8, ["doqquzuncu"] = 9, ["onuncu"] = 10,
        ["on birinci"] = 11, ["on ikinci"] = 12,
    };

    public static int? Parse(string? lane)
    {
        if (string.IsNullOrWhiteSpace(lane))
        {
            return null;
        }

        var text = lane
            .Replace("döngə", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("donge", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        // "5-ci", "5ci", "5." and plain "5" all start with the digits that matter.
        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length > 0 && int.TryParse(digits, out var parsed))
        {
            return parsed;
        }

        // Spelled out. Trailing ordinal suffixes are already part of the words above, so this is
        // a straight lookup rather than another round of stripping.
        return SpelledOut.TryGetValue(text.Trim(), out var spoken) ? spoken : null;
    }
}
