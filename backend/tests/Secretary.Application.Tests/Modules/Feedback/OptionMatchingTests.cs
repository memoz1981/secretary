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

    private void Given(SurveyQuestion question)
    {
        WithId(question, 10);

        var id = 100;
        foreach (var option in question.Options)
        {
            WithId(option, id++);
        }

        _questions.Setup(q => q.GetWithOptionsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(question);
    }

    private void GivenChoice(params string[] labels)
        => Given(SurveyQuestion.ChoiceQuestion(1, 0, "Nə üçün gəldiniz?", labels, false, Now));

    private void GivenScale(int scaleMax)
        => Given(SurveyQuestion.ScaleQuestion(1, 0, "Qiymətləndirin", scaleMax, true, Now));

    private void GivenYesNo()
        => Given(SurveyQuestion.YesNoQuestion(1, 0, "Məmnun qaldınız?", true, true, Now));

    /// <summary>⚠ The one a real call found: the caller said "hə" and was refused twice, on the
    /// question type that should be hardest to get wrong. "Bəli" is the written word and the
    /// stored label; it is not what anybody says.</summary>
    [Theory]
    [InlineData("hə", "Bəli")]
    [InlineData("əlbəttə", "Bəli")]
    [InlineData("bəli", "Bəli")]
    [InlineData("yox", "Xeyr")]
    [InlineData("xeyr", "Xeyr")]
    public async Task Yes_and_no_match_however_they_are_said(string spoken, string expected)
    {
        GivenYesNo();

        var match = await _sut.MatchOptionAsync(10, spoken, default);

        match.ShouldNotBeNull();
        match!.Text.ShouldBe(expected);
    }

    /// <summary>The synonyms are for Yes/No questions alone. On a list of names, "yox" is not an
    /// option and pretending otherwise would file an answer against something nobody offered.</summary>
    [Fact]
    public async Task Yes_and_no_words_do_not_leak_into_a_named_list()
    {
        GivenChoice("Təmir", "Satış");

        (await _sut.MatchOptionAsync(10, "hə", default)).ShouldBeNull();
    }

    /// <summary>The fold that bites everywhere text is compared here — ş ə ç ğ ı ö ü. A caller
    /// saying the word correctly must match an option somebody typed in ASCII.</summary>
    [Theory]
    [InlineData("Yaxşı")]
    [InlineData("yaxsi")]
    [InlineData("YAXŞI")]
    public async Task An_option_matches_however_the_azerbaijani_is_spelled(string spoken)
    {
        GivenChoice("Yaxşı", "Pis");

        var match = await _sut.MatchOptionAsync(10, spoken, default);

        match.ShouldNotBeNull();
        match!.Text.ShouldBe("Yaxşı");
    }

    /// <summary>⚠ The case a real call found, and the reason scores are derived now.
    ///
    /// A 1-5 was set up carrying 20/40/60/80/100, the matcher compared the spoken number to that,
    /// and "dörd" matched nothing — on the one question people always answer with a number. The
    /// original test used 1-5 for both the label and the score, where the two happen to agree, so
    /// it passed on the broken code. Matching is against the TEXT, and the score is now a
    /// percentage nobody would ever say out loud.</summary>
    [Theory]
    [InlineData("4", "4")]
    [InlineData("dörd", "4")]
    [InlineData("beş", "5")]
    [InlineData("bir", "1")]
    public async Task A_number_matches_whether_it_is_said_as_a_word_or_a_digit(string spoken, string expected)
    {
        GivenScale(5);

        var match = await _sut.MatchOptionAsync(10, spoken, default);

        match.ShouldNotBeNull();
        match!.Text.ShouldBe(expected);
    }

    /// <summary>The score behind "4" on a five-point scale is 75%, and nobody says "yetmiş beş".
    /// Pinned so a future matcher cannot quietly start reading the score again.</summary>
    [Fact]
    public async Task The_percentage_behind_an_option_is_never_what_matches()
    {
        GivenScale(5);

        (await _sut.MatchOptionAsync(10, "yetmiş beş", default)).ShouldBeNull();
    }

    /// <summary>⚠ Number words only match options that carry numbers. Otherwise "iki" would pick
    /// the second item of a list that has nothing to do with counting — a caller answering a
    /// question about which service they used would be filed as having said "2".</summary>
    [Fact]
    public async Task A_number_word_does_not_match_an_unnumbered_list()
    {
        GivenChoice("Təmir", "Satış", "Servis");

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
        GivenChoice("Yaxşı", "Pis");

        (await _sut.MatchOptionAsync(10, spoken, default)).ShouldBeNull();
    }

    /// <summary>People answer in sentences, not single words.</summary>
    [Fact]
    public async Task An_option_inside_a_sentence_still_matches()
    {
        GivenChoice("Yaxşı", "Pis");

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
