using Secretary.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>An address reaches us as speech, and these three are what turn it into something
/// comparable. They decide whether a returning customer is recognised or gets a second record —
/// and, for the lane, whether a delivery goes to the right house.</summary>
public sealed class AddressMatchingTests
{
    [Theory]
    [InlineData("Nəsimi", "Nəsimi")]
    [InlineData("Nəsimi rayonu", "Nəsimi")]
    [InlineData("nesimi", "Nəsimi")]
    [InlineData("NESIMI", "Nəsimi")]
    [InlineData("Sabunçu", "Sabunçu")]
    [InlineData("sabuncu", "Sabunçu")]
    [InlineData("Pirallahı", "Pirallahı")]
    [InlineData("pirallahi", "Pirallahı")]
    public void A_rayon_said_any_way_reaches_one_spelling(string spoken, string expected)
        => BakuDistricts.Match(spoken).ShouldBe(expected);

    [Theory]
    [InlineData("Gəncə")]
    [InlineData("Xırdalan")]
    [InlineData("")]
    [InlineData(null)]
    public void Somewhere_that_is_not_a_Baku_rayon_does_not_match_one(string? spoken)
        => BakuDistricts.Match(spoken).ShouldBeNull();

    [Fact]
    public void All_twelve_rayons_match_themselves()
        => BakuDistricts.All.ShouldAllBe(d => BakuDistricts.Match(d) == d);

    [Theory]
    [InlineData("5-ci döngə", 5)]
    [InlineData("5ci döngə", 5)]
    [InlineData("5 döngə", 5)]
    [InlineData("beşinci döngə", 5)]
    [InlineData("besinci donge", 5)]
    [InlineData("12-ci döngə", 12)]
    [InlineData("3", 3)]
    public void The_same_lane_said_three_ways_is_one_number(string spoken, int expected)
        => LaneNumber.Parse(spoken).ShouldBe(expected);

    /// <summary>Named lanes exist. They fall back to text comparison rather than being forced
    /// into a number.</summary>
    [Theory]
    [InlineData("Gül döngəsi")]
    [InlineData("")]
    [InlineData(null)]
    public void A_lane_with_no_number_in_it_has_none(string? spoken)
        => LaneNumber.Parse(spoken).ShouldBeNull();

    [Theory]
    [InlineData("Ə. Cavad küçəsi", "Ə. Cavad küç.")]
    [InlineData("Ə. Cavad küçəsi", "e.cavad")]
    [InlineData("Nizami prospekti", "Nizami pr")]
    [InlineData("28 May küçəsi", "28 may")]
    public void One_street_written_two_ways_folds_to_one_key(string a, string b)
        => AddressText.Normalize(a).ShouldBe(AddressText.Normalize(b));

    [Fact]
    public void Two_different_streets_do_not_collide()
        => AddressText.Normalize("Nizami küçəsi").ShouldNotBe(AddressText.Normalize("Nərimanov küçəsi"));

    /// <summary>The Azerbaijani casing trap, as its own test because it fails silently.
    /// ToLowerInvariant turns "İ" into an i with a combining dot and leaves "I" as "i" rather
    /// than "ı", so a street stops matching itself and nobody notices until a customer is
    /// duplicated.</summary>
    [Theory]
    [InlineData("İnşaatçılar prospekti", "inşaatçılar prospekti")]
    [InlineData("İnşaatçılar prospekti", "insaatcilar pr")]
    [InlineData("Işıqlı küçəsi", "ışıqlı küçəsi")]
    public void Dotted_and_dotless_i_do_not_split_a_street_in_two(string a, string b)
        => AddressText.Normalize(a).ShouldBe(AddressText.Normalize(b));

    [Theory]
    [InlineData("12A", "12a")]
    [InlineData("12/3", "123")]
    public void Building_numbers_fold_the_same_way(string spoken, string expected)
        => AddressText.Normalize(spoken).ShouldBe(expected);
}
