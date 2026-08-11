using Secretary.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests;

/// <summary>A caller said "Şirab" — correctly — and was told the business does not sell it,
/// because the catalogue spells it "Sirab" with a Latin S and the comparison was ordinal. Product
/// names will differ from however someone typed them in by exactly these letters, every time.
///
/// The matcher folds through AddressText, so these tests pin the folding that matters for
/// products rather than the lookup around it.</summary>
public sealed class ProductMatchingTests
{
    [Theory]
    [InlineData("Şirab", "Sirab")]
    [InlineData("şirab", "SIRAB")]
    [InlineData("Çörək", "Corek")]
    [InlineData("Ət", "Et")]
    [InlineData("Süd", "Sud")]
    [InlineData("Yağ", "Yag")]
    [InlineData("Qırmızı", "Qirmizi")]
    public void A_name_said_in_Azerbaijani_folds_to_the_way_it_was_typed(string spoken, string typed)
        => AddressText.Normalize(spoken).ShouldBe(AddressText.Normalize(typed));

    /// <summary>Folding must not make everything match everything.</summary>
    [Fact]
    public void Two_different_products_still_differ()
        => AddressText.Normalize("Sirab").ShouldNotBe(AddressText.Normalize("Çörək"));

    /// <summary>The alias field is where "bidon" lives, and it arrives as the caller says it.</summary>
    [Theory]
    [InlineData("bidon", "Bidon")]
    [InlineData("BALON", "balon")]
    public void Aliases_fold_the_same_way(string spoken, string stored)
        => AddressText.Normalize(spoken).ShouldBe(AddressText.Normalize(stored));
}
