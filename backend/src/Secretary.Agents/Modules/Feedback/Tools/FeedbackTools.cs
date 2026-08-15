using System.ComponentModel;
using Secretary.Agents.Feedback;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Domain.Enums;

namespace Secretary.Agents.Tools;

/// <summary>The survey line's tools. Results are DATA — never sentences telling the model how to
/// behave, because anything phrased as instruction gets read aloud to the caller.
///
/// Three tools and none of them takes a question id. The service knows which question is next,
/// because the agent works strictly in order, so there is no id for the model to carry and
/// therefore no way to file an answer under the wrong question. That is the same lesson as the
/// order line's address id, applied before it could bite: a model asked to copy an identifier out
/// of a conversation will eventually copy the wrong one.
///
/// Nor does the model decide which option was chosen. It passes on what it heard and the service
/// matches it against the stored options — a model left to judge that will confidently record an
/// answer nobody gave.</summary>
public sealed class FeedbackTools
{
    private readonly FeedbackCallService _calls;
    private readonly FeedbackCallSession _session;
    private readonly EscalationTools _escalation;

    public FeedbackTools(FeedbackCallService calls, FeedbackCallSession session, EscalationTools escalation)
    {
        _calls = calls;
        _session = session;
        _escalation = escalation;
    }

    [Description("The next question to ask, with its options. Call it once, ask what it returns, then record the "
                 + "answer before calling it again.")]
    public async Task<string> GetNextQuestion()
    {
        var question = await _calls.GetNextQuestionAsync(_session.Require(), default);
        if (question is null)
        {
            return "SURVEY_DONE.";
        }

        if (question.QuestionType == FeedbackQuestionType.Open)
        {
            return $"OPEN_QUESTION. {question.Text}";
        }

        var options = string.Join("; ", question.Options.Select(o => o.Text));
        return $"CHOICE_QUESTION. {question.Text} Variantlar: {options}";
    }

    [Description("Records what the caller answered to the question you just asked. Pass their words exactly as "
                 + "they said them.")]
    public async Task<string> RecordAnswer(
        [Description("Exactly what the caller said, in their own words")] string spokenAnswer)
    {
        var callId = _session.Require();
        var question = await _calls.GetNextQuestionAsync(callId, default);
        if (question is null)
        {
            return "SURVEY_DONE.";
        }

        if (string.IsNullOrWhiteSpace(spokenAnswer))
        {
            return "NOTHING_HEARD.";
        }

        // Recorded verbatim. This is the only place a survey keeps the caller's own words, and
        // it is the reason this module turns caller transcription on when the others leave it
        // off — without the transcript there is nothing to store.
        if (question.QuestionType == FeedbackQuestionType.Open)
        {
            await _calls.RecordOpenAsync(callId, question.QuestionId, spokenAnswer, default);
            return "RECORDED.";
        }

        var option = await _calls.MatchOptionAsync(question.QuestionId, spokenAnswer, default);
        if (option is null)
        {
            // Not an answer we can file. Saying so is the whole point — the alternative is the
            // model picking the nearest option and a number on a dashboard that nobody said.
            return "NO_MATCH. Variantlar: " + string.Join("; ", question.Options.Select(o => o.Text));
        }

        await _calls.RecordChoiceAsync(callId, question.QuestionId, option.Id, default);
        return $"RECORDED. {option.Text}";
    }

    [Description("Records that the caller would rather not answer this question. Only after they have made that "
                 + "clear — never to move things along.")]
    public async Task<string> SkipQuestion()
    {
        var callId = _session.Require();
        var question = await _calls.GetNextQuestionAsync(callId, default);
        if (question is null)
        {
            return "SURVEY_DONE.";
        }

        // A declined answer is a row, not an absence. A missing row also means the call ended
        // before this question, and the dashboard has to tell somebody who said no from somebody
        // who hung up.
        await _calls.RecordDeclineAsync(callId, question.QuestionId, default);
        return "RECORDED.";
    }

    [Description("Transfers the caller to a person. For anyone who asks for one, or is upset enough that a survey "
                 + "is the wrong thing to be doing.")]
    public async Task<string> EscalateToHuman(
        [Description("Their phone number")] string callerPhoneNumber,
        [Description("Why, in one line")] string reason)
        => await _escalation.EscalateToHuman(callerPhoneNumber, reason);
}
