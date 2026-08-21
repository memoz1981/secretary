using Secretary.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>Yes and no, in the ways people say them rather than the way they are stored.
///
/// ⚠ A real caller answered "hə" to a Bəli/Xeyr question, was refused twice, and the call ended.
/// Nobody says "bəli" out loud — it is the written form and the stored label — so matching the
/// label alone made the question type that should never fail the one that failed first.</summary>
public sealed class YesNoWordsTests
{
    [Theory]
    [InlineData("hə")]
    [InlineData("Hə")]
    [InlineData("he")]
    [InlineData("bəli")]
    [InlineData("Bəli.")]
    [InlineData("əlbəttə")]
    [InlineData("hə, razıyam")]
    [InlineData("da")]
    [InlineData("yes")]
    public void Every_way_of_saying_yes_reads_as_yes(string spoken)
        => YesNoWords.Read(spoken).ShouldBe(true);

    [Theory]
    [InlineData("xeyr")]
    [InlineData("yox")]
    [InlineData("Yox.")]
    [InlineData("yoxdur")]
    [InlineData("xeyir")]
    [InlineData("net")]
    [InlineData("no")]
    public void Every_way_of_saying_no_reads_as_no(string spoken)
        => YesNoWords.Read(spoken).ShouldBe(false);

    /// <summary>⚠ The reason this folds into words rather than one run of characters, the way
    /// addresses do. "hər" folds to "her", and "hər şey yaxşı idi" run together contains "he" —
    /// which would read a sentence about everything being fine as the word "yes", correctly by
    /// accident here and wrongly the moment the sentence says something else.</summary>
    [Theory]
    [InlineData("hər şey yaxşı idi")]
    [InlineData("heç nə demirəm")]
    [InlineData("bilmirəm")]
    [InlineData("")]
    public void A_sentence_that_merely_contains_the_letters_is_not_an_answer(string spoken)
        => YesNoWords.Read(spoken).ShouldBeNull();

    /// <summary>Both words in one breath is not an answer either. Taking whichever the loop
    /// reached first would record a real answer on a coin toss.</summary>
    [Theory]
    [InlineData("hə, yox, gözləmədim")]
    [InlineData("yox, hə")]
    public void Saying_both_is_no_answer_at_all(string spoken)
        => YesNoWords.Read(spoken).ShouldBeNull();
}
