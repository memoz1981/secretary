using System.Text;

namespace Secretary.Domain.ValueObjects;

/// <summary>Puts a phone number into one canonical form, so the same caller is the same record.
///
/// Numbers reach us as speech: the agent transcribes what the caller reads out, and the same
/// person says "0 55 355 88 32" one day and "+994 55 355 88 32" the next. Stored verbatim, those
/// are two different strings, and a returning caller is not recognised — which then leads to a
/// second client row for one person. That is exactly what happened on the first Gemini calls:
/// two "Mehdi" records, one "0501112233" and one "050 111 22 33".
///
/// Deliberately conservative. Azerbaijani numbers are canonicalised to E.164; anything that does
/// not look Azerbaijani only loses its separators, because mangling a number we do not recognise
/// is worse than storing it as given.</summary>
public static class PhoneNumberNormalizer
{
    private const string CountryCode = "994";

    /// <summary>Azerbaijani subscriber numbers are nine digits after the country code.</summary>
    private const int SubscriberDigits = 9;

    public static string Normalize(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var digits = new StringBuilder(phoneNumber.Length);
        foreach (var c in phoneNumber)
        {
            if (char.IsDigit(c))
            {
                digits.Append(c);
            }
        }

        var value = digits.ToString();
        if (value.Length == 0)
        {
            return phoneNumber.Trim();
        }

        // 994XXXXXXXXX — already international, with or without the plus.
        if (value.Length == CountryCode.Length + SubscriberDigits && value.StartsWith(CountryCode, StringComparison.Ordinal))
        {
            return $"+{value}";
        }

        // 0XXXXXXXXX — the way a local number is spoken and written.
        if (value.Length == SubscriberDigits + 1 && value[0] == '0')
        {
            return $"+{CountryCode}{value[1..]}";
        }

        // XXXXXXXXX — the subscriber number alone, no trunk prefix.
        //
        // Skipped when it already begins 994, because that case is genuinely ambiguous: "99" is
        // itself a valid Azerbaijani mobile prefix, so 994000000 could be a subscriber number
        // OR a country code with a short number behind it. Guessing turned +994000000 into
        // +994994000000. Leaving an ambiguous number alone is the lesser harm.
        if (value.Length == SubscriberDigits && !value.StartsWith(CountryCode, StringComparison.Ordinal))
        {
            return $"+{CountryCode}{value}";
        }

        // Not a shape we recognise. Keep the digits, keep any leading plus, change nothing else.
        return phoneNumber.TrimStart().StartsWith('+') ? $"+{value}" : value;
    }
}
