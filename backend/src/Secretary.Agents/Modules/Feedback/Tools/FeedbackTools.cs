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

    [Description("The next question to ask. Call it once, ask what it returns, then record the answer before "
                 + "calling it again.")]
    public async Task<string> GetNextQuestion()
    {
        var question = await _calls.GetNextQuestionAsync(_session.Require(), default);
        if (question is null)
        {
            return "SURVEY_DONE.";
        }

        // Remembered, so RecordAnswer can refuse an answer to a question that was never handed
        // over. See FeedbackCallSession.ServedQuestionId.
        _session.Served(question.QuestionId, question.QuestionType == FeedbackQuestionType.Open);

        // ⚠ Only a Choice carries its options here, and that is deliberate. A yes/no question
        // already contains its answers, and reading "1, 2, 3, 4, 5" after "birdən beşə qədər" is
        // the readback a caller sat through five times before hanging up. What is not in the
        // marker cannot be read out.
        return question.QuestionType switch
        {
            FeedbackQuestionType.Open => $"OPEN_QUESTION. {question.Text}",
            FeedbackQuestionType.YesNo => $"YES_NO_QUESTION. {question.Text}",
            FeedbackQuestionType.Scale => $"SCALE_QUESTION. {question.Text} (1-{question.ScaleMax})",
            // ⚠ "Digər" is left out of the list on purpose. Reading it aloud invites the caller
            // to answer with the word instead of with the thing: one said "digərini, digər
            // sözün" and the survey recorded "Digər" with nothing behind it, having already
            // discarded the real answer they gave a moment earlier. It is a catch-all, not a
            // choice — anything off the list becomes it, with their own words attached.
            _ => $"CHOICE_QUESTION. {question.Text} Variantlar: "
                 + string.Join("; ", question.Options.Where(o => !o.IsOther).Select(o => o.Text)),
        };
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

        // ⚠ An answer to a question nobody was asked. On a real call the agent skipped
        // GetNextQuestion entirely, made a rating question up, and the caller's "4" was filed
        // against "Məmnun qaldınız?" — Bəli or Xeyr — where it could not match. Two failures
        // later the call ended, on a question the caller had never heard.
        //
        // Deliberately not counted as a failure: the caller did nothing wrong, and ending the
        // call over the agent's own bookkeeping would be the same bug wearing a different hat.
        if (_session.ServedQuestionId != question.QuestionId)
        {
            // Unless they are still talking. An open answer arrives in pieces — the caller
            // pauses, the provider ends the turn, the agent records what it has, and the rest of
            // the sentence turns up next. That is not a stray answer, it is the same one
            // continuing, and it used to be thrown away as "never asked".
            return _session is { ServedIsOpen: true, ServedQuestionId: { } servedId }
                   && await _calls.ExtendOpenAnswerAsync(callId, servedId, spokenAnswer, default)
                ? "RECORDED."
                : "NO_QUESTION_ASKED.";
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
        if (option is not null)
        {
            // ⚠ They named the catch-all rather than saying what it was. "Digər" on its own is a
            // tally with nothing behind it — the whole reason for offering it is that the list
            // was incomplete, so the answer is the part that is missing. Ask, once; if they say
            // it again, take it bare rather than argue, because a thin answer beats no answer.
            if (option.IsOther && !_session.TooManyFailuresFor(question.QuestionId))
            {
                return "OTHER_NEEDS_WORDS.";
            }

            if (option.IsOther)
            {
                await _calls.RecordOtherAsync(callId, question.QuestionId, option.Id, spokenAnswer, default);
                return "RECORDED.";
            }

            await _calls.RecordChoiceAsync(callId, question.QuestionId, option.Id, default);
            return $"RECORDED. {option.Text}";
        }

        // A question that allows "Digər" has no unmatchable answer — that is what allowing it
        // means. Their words go in beside the option, because a pile of undifferentiated "other"
        // is a count with nothing behind it.
        if (question.Options.FirstOrDefault(o => o.IsOther) is { } other)
        {
            await _calls.RecordOtherAsync(callId, question.QuestionId, other.Id, spokenAnswer, default);
            return "RECORDED.";
        }

        // Not an answer we can file. Saying so is the whole point — the alternative is the model
        // picking the nearest option and a number on a dashboard that nobody said.
        //
        // ⚠ But the asking stops after the second go, in code and not by instruction. One real
        // call read the same five options back five times because nothing said when to give up.
        // The survey does not limp on either: a caller whose answers keep failing to land is not
        // going to be understood on question four, so a person rings them instead.
        if (_session.TooManyFailuresFor(question.QuestionId))
        {
            await _calls.HandOverToHumanAsync(callId, default);
            return "CANNOT_CONTINUE.";
        }

        return question.QuestionType == FeedbackQuestionType.Choice
            ? "NO_MATCH. Variantlar: " + string.Join("; ", question.Options.Select(o => o.Text))
            : "NO_MATCH.";
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

        if (_session.ServedQuestionId != question.QuestionId)
        {
            return "NO_QUESTION_ASKED.";
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
