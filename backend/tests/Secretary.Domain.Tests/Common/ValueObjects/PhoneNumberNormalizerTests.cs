using Secretary.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>The reason this exists: two "Mehdi" client rows, one "0535353535" and one
/// "050 111 22 33", because a number reaches us as speech and gets read out differently each
/// time. Worth tests of its own — the first version of it turned "+994000000" into
/// "+994994000000", which the Client tests caught only by accident.</summary>
public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    // Local trunk form, however it is spaced or punctuated.
    [InlineData("0553558832", "+994553558832")]
    [InlineData("055 355 88 32", "+994553558832")]
    [InlineData("055-355-88-32", "+994553558832")]
    [InlineData("(055) 355 88 32", "+994553558832")]
    // Already international, with or without the plus.
    [InlineData("+994553558832", "+994553558832")]
    [InlineData("994553558832", "+994553558832")]
    [InlineData("+994 55 355 88 32", "+994553558832")]
    // Subscriber number alone.
    [InlineData("553558832", "+994553558832")]
    public void Azerbaijani_numbers_reach_one_canonical_form(string input, string expected)
        => PhoneNumberNormalizer.Normalize(input).ShouldBe(expected);

    [Fact]
    public void The_same_caller_spelling_it_three_ways_is_one_number()
    {
        var forms = new[] { "0553558832", "+994 55 355 88 32", "994553558832" };

        forms.Select(PhoneNumberNormalizer.Normalize).Distinct().Count().ShouldBe(1);
    }

    /// <summary>"99" is a real Azerbaijani mobile prefix, so nine digits beginning 994 could be
    /// a subscriber number or a country code with a short number behind it. Guessing produced
    /// +994994000000. An ambiguous number is left alone.</summary>
    [Theory]
    [InlineData("+994000000", "+994000000")]
    [InlineData("994000000", "994000000")]
    public void Ambiguous_numbers_are_left_as_they_came(string input, string expected)
        => PhoneNumberNormalizer.Normalize(input).ShouldBe(expected);

    [Theory]
    // Not a shape we recognise: keep the digits, keep the plus, change nothing else. Mangling a
    // foreign number is worse than storing it as given.
    [InlineData("+44 20 7946 0958", "+442079460958")]
    [InlineData("12345", "12345")]
    public void Unrecognised_shapes_only_lose_their_separators(string input, string expected)
        => PhoneNumberNormalizer.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_in_nothing_out(string? input)
        => PhoneNumberNormalizer.Normalize(input).ShouldBe(string.Empty);

    [Fact]
    public void A_number_with_no_digits_at_all_is_returned_trimmed()
        => PhoneNumberNormalizer.Normalize("  n/a  ").ShouldBe("n/a");
}
