using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Pricing;
using Secretary.Application.Services;
using Secretary.Domain.Abstractions;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests;

/// <summary>Which option the caller picked is decided here and never by the model.
///
/// The reason is the same one the order line learned: a model asked to judge will confidently
/// produce an answer, and a wrong option is a number on a dashboard that nobody said. So the
/// matching has to handle how people actually answer — folded Azerbaijani, and a number said as
/// a word — and it has to say no when it does not know.</summary>
public sealed class OptionMatchingTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISurveyQuestionRepository> _questions = new();
    private readonly FeedbackCallService _sut;

    public OptionMatchingTests()
    {
        _uow.SetupGet(u => u.SurveyQuestions).Returns(_questions.Object);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.GetCurrentInstant()).Returns(Now);

        var tenant = new Mock<ICurrentTenantProvider>();
        tenant.SetupGet(t => t.TenantId).Returns(1);

        _sut = new FeedbackCallService(
            _uow.Object, clock.Object, tenant.Object,
            new TokenPricebook(Options.Create(new ModelPricingOptions())));
    }

    private void GivenQuestion(params (string Text, int? Value)[] options)
    {
        var question = SurveyQuestion.Create(1, 0, "Qiymətləndirin", FeedbackQuestionType.Choice, false, Now);
        WithId(question, 10);

        var id = 100;
        foreach (var (text, value) in options)
        {
            question.AddOption(text, value, Now);
            WithId(question.Options[^1], id++);
        }

        _questions.Setup(q => q.GetWithOptionsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(question);
    }

    /// <summary>The fold that bites everywhere text is compared here — ş ə ç ğ ı ö ü. A caller
    /// saying the word correctly must match an option somebody typed in ASCII.</summary>
    [Theory]
    [InlineData("Yaxşı")]
    [InlineData("yaxsi")]
    [InlineData("YAXŞI")]
    public async Task An_option_matches_however_the_azerbaijani_is_spelled(string spoken)
    {
        GivenQuestion(("Yaxşı", null), ("Pis", null));

        var match = await _sut.MatchOptionAsync(10, spoken, default);

        match.ShouldNotBeNull();
        match!.Text.ShouldBe("Yaxşı");
    }

    /// <summary>A 1-5 scale is answered out loud, and a transcript gives either form.</summary>
    [Theory]
    [InlineData("4", 4)]
    [InlineData("dörd", 4)]
    [InlineData("beş", 5)]
    [InlineData("bir", 1)]
    public async Task A_number_matches_whether_it_is_said_as_a_word_or_a_digit(string spoken, int expected)
    {
        GivenQuestion(("1", 1), ("2", 2), ("3", 3), ("4", 4), ("5", 5));

        var match = await _sut.MatchOptionAsync(10, spoken, default);

        match.ShouldNotBeNull();
        match!.Value.ShouldBe(expected);
    }

    /// <summary>⚠ The one this test file originally missed, and a real call found.
    ///
    /// The value is a SCORE the business chose — this question was set up 20/40/60/80/100 — and
    /// has nothing to do with what the caller says. Matching "dörd" against the value found
    /// nothing, so every spoken number was refused on the one question people always answer with
    /// a number. The original test used values 1-5 and passed happily.</summary>
    [Theory]
    [InlineData("dörd", "4")]
    [InlineData("3", "3")]
    [InlineData("beş", "5")]
    public async Task A_spoken_number_matches_the_option_text_whatever_the_score_behind_it_is(
        string spoken, string expected)
    {
        GivenQuestion(("1", 20), ("2", 40), ("3", 60), ("4", 80), ("5", 100));

        var match = await _sut.MatchOptionAsync(10, spoken, default);

        match.ShouldNotBeNull();
        match!.Text.ShouldBe(expected);
    }

    /// <summary>⚠ Number words only match options that carry numbers. Otherwise "iki" would pick
    /// the second item of a list that has nothing to do with counting — a caller answering a
    /// question about which service they used would be filed as having said "2".</summary>
    [Fact]
    public async Task A_number_word_does_not_match_an_unnumbered_list()
    {
        GivenQuestion(("Təmir", null), ("Satış", null), ("Digər", null));

        (await _sut.MatchOptionAsync(10, "iki", default)).ShouldBeNull();
    }

    /// <summary>Saying no is the whole point. The agent asks again; the alternative is the
    /// nearest option and a number nobody gave.</summary>
    [Theory]
    [InlineData("bilmirəm")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Nothing_recognisable_matches_nothing(string spoken)
    {
        GivenQuestion(("Yaxşı", null), ("Pis", null));

        (await _sut.MatchOptionAsync(10, spoken, default)).ShouldBeNull();
    }

    /// <summary>People answer in sentences, not single words.</summary>
    [Fact]
    public async Task An_option_inside_a_sentence_still_matches()
    {
        GivenQuestion(("Yaxşı", null), ("Pis", null));

        var match = await _sut.MatchOptionAsync(10, "hər şey yaxşı idi", default);

        match.ShouldNotBeNull();
        match!.Text.ShouldBe("Yaxşı");
    }

    private static T WithId<T>(T entity, int id)
        where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(entity, id);
        return entity;
    }
}
