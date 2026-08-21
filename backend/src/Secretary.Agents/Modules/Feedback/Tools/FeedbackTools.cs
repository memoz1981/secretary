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
/// Nor does the model decide which option was chosen, and nor does it supply the caller's words.
/// Both come from the transcription: a model asked to report what somebody said reports a tidied
/// version, and on a real call "Ömür əllərim belə çox gözləmədim" was stored as "Ümumi rəylərim.
/// Belə, çox gözləmədim" — the model's expectation, in the one field that exists to hold the
/// caller's own words. What it passes is now a fallback for when there is no transcription.
///
/// ⚠ Every label in a result is UPPERCASE and English, and that is not decoration. The Azerbaijani
/// word "Variantlar:" was used as a separator once and the agent read it out to the caller as
/// though it were part of the question. A label the model would be embarrassed to say aloud is a
/// label it does not say aloud.</summary>
public sealed class FeedbackTools
{
    private readonly FeedbackCallService _calls;
    private readonly FeedbackCallSession _session;
    private readonly CallerSpeech _speech;
    private readonly EscalationTools _escalation;

    public FeedbackTools(
        FeedbackCallService calls, FeedbackCallSession session, CallerSpeech speech,
        EscalationTools escalation)
    {
        _calls = calls;
        _session = session;
        _speech = speech;
        _escalation = escalation;
    }

    [Description("The next question to ask. Only needed to start, or if you have lost your place — recording an "
                 + "answer already hands you the one after it.")]
    public async Task<string> GetNextQuestion()
    {
        return await Describe(await _calls.GetNextQuestionAsync(_session.Require(), default));
    }

    /// <summary>The first question, written as a line of instructions rather than as a tool
    /// result, for the greeting.
    ///
    /// ⚠ The marker form went in here first — "YES_NO_QUESTION ASK: …" — and putting a tool
    /// result inside the instructions is asking for it to be read out, which is the mistake this
    /// module keeps making in different clothes. Markers belong in results; instructions are
    /// prose.</summary>
    internal async Task<string> FirstQuestionForGreeting()
    {
        var question = await _calls.GetNextQuestionAsync(_session.Require(), default);
        if (question is null)
        {
            return "There are no questions to ask. Apologise briefly and end the call.";
        }

        _ = await Describe(question);

        var note = question.QuestionType switch
        {
            FeedbackQuestionType.YesNo => " It is a yes/no question — do not read the answers out.",
            FeedbackQuestionType.Scale =>
                $" Say the range as words — birdən {question.ScaleMax}-ə qədər — and never count the numbers out.",
            FeedbackQuestionType.Choice =>
                " Read these options after it, pausing between them: "
                + string.Join("; ", question.Options.Where(o => !o.IsOther).Select(o => o.Text)),
            _ => " Let them answer in their own words.",
        };

        return $"Then ask this, and nothing else: \"{question.Text}\".{note}";
    }

    /// <summary>One question, in the shape the agent is meant to ask it — and the bookkeeping
    /// that goes with handing it over.</summary>
    private async Task<string> Describe(FeedbackQuestionForAgent? question)
    {
        await Task.CompletedTask;

        if (question is null)
        {
            return "SURVEY_DONE.";
        }

        // Remembered, so RecordAnswer can refuse an answer to a question that was never handed
        // over. See FeedbackCallSession.ServedQuestionId.
        _session.Served(question.QuestionId, question.QuestionType == FeedbackQuestionType.Open);

        // Everything the caller says from here is the answer to this question.
        _speech.StartOfAnswer();

        // ⚠ Only a Choice carries its options here, and that is deliberate. A yes/no question
        // already contains its answers, and reading "1, 2, 3, 4, 5" after "birdən beşə qədər" is
        // the readback a caller sat through five times before hanging up. What is not in the
        // marker cannot be read out.
        return question.QuestionType switch
        {
            FeedbackQuestionType.Open => $"OPEN_QUESTION ASK: {question.Text}",
            FeedbackQuestionType.YesNo => $"YES_NO_QUESTION ASK: {question.Text}",
            FeedbackQuestionType.Scale =>
                $"SCALE_QUESTION ASK: {question.Text} RANGE: 1-{question.ScaleMax}",
            // ⚠ "Digər" is left out of the list on purpose. Reading it aloud invites the caller
            // to answer with the word instead of with the thing: one said "digərini, digər
            // sözün" and the survey recorded "Digər" with nothing behind it, having already
            // discarded the real answer they gave a moment earlier. It is a catch-all, not a
            // choice — anything off the list becomes it, with their own words attached.
            _ => $"CHOICE_QUESTION ASK: {question.Text} OPTIONS: "
                 + string.Join("; ", question.Options.Where(o => !o.IsOther).Select(o => o.Text)),
        };
    }

    [Description("Records the caller's answer to the question you just asked. Call it once they have finished "
                 + "speaking.")]
    public async Task<string> RecordAnswer(
        [Description("What you heard them say. Used only if the line's own transcription failed.")]
        string spokenAnswer)
    {
        var callId = _session.Require();
        var question = await _calls.GetNextQuestionAsync(callId, default);
        if (question is null)
        {
            return "SURVEY_DONE.";
        }

        // ⚠ The transcription first, the model's report second. A model asked what somebody said
        // answers with a tidied version of it — "Ömür əllərim belə çox gözləmədim" came back as
        // "Ümumi rəylərim. Belə, çox gözləmədim", which is a plausible sentence nobody uttered.
        // The provider already sends us the words; the argument is for when it does not.
        var said = _speech.SinceQuestion() ?? spokenAnswer;

        if (string.IsNullOrWhiteSpace(said))
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
                   && await _calls.ExtendOpenAnswerAsync(callId, servedId, said, default)
                ? "RECORDED."
                : "NO_QUESTION_ASKED.";
        }

        // Recorded verbatim. This is the only place a survey keeps the caller's own words, and
        // it is the reason this module turns caller transcription on when the others leave it
        // off — without the transcript there is nothing to store.
        if (question.QuestionType == FeedbackQuestionType.Open)
        {
            await _calls.RecordOpenAsync(callId, question.QuestionId, said, default);
            return await RecordedAndNext(callId);
        }

        var option = await _calls.MatchOptionAsync(question.QuestionId, said, default);
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
                await _calls.RecordOtherAsync(callId, question.QuestionId, option.Id, said, default);
                return await RecordedAndNext(callId);
            }

            await _calls.RecordChoiceAsync(callId, question.QuestionId, option.Id, default);
            return await RecordedAndNext(callId);
        }

        // A question that allows "Digər" has no unmatchable answer — that is what allowing it
        // means. Their words go in beside the option, because a pile of undifferentiated "other"
        // is a count with nothing behind it.
        if (question.Options.FirstOrDefault(o => o.IsOther) is { } other)
        {
            await _calls.RecordOtherAsync(callId, question.QuestionId, other.Id, said, default);
            return await RecordedAndNext(callId);
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
            ? "NO_MATCH. OPTIONS: " + string.Join("; ", question.Options.Where(o => !o.IsOther).Select(o => o.Text))
            : "NO_MATCH.";
    }

    /// <summary>Confirms the answer and hands over the next question in one result.
    ///
    /// ⚠ This is a latency fix and a cost fix, and they are the same fix. Recording an answer and
    /// fetching the next question were two tool calls, and on this provider a tool call is a
    /// whole model invocation carrying the entire prompt — about 4,900 text tokens of
    /// instructions, every time. Measured on a real call, the second round trip added ~1.5 s to
    /// every question and there were eight of them.
    ///
    /// The order line learned the same thing: one PlaceOrder with everything in it, rather than a
    /// ladder of calls each costing a turn.</summary>
    /// ⚠ The matched option is deliberately NOT named in the result. It was, for one round, and
    /// the agent read it back: "Bəli. Bir-dən ona qədər neçə xal…", "Bir. Sizə servis kitabçası…".
    /// The instructions say not to repeat an answer back, and a value put in front of the model is
    /// worth more than a rule about not saying it. Nothing needed the name; it was there to make
    /// the log easier to read, and the log has the tool call beside it anyway.
    private async Task<string> RecordedAndNext(int callId)
        => $"RECORDED. NEXT — {await Describe(await _calls.GetNextQuestionAsync(callId, default))}";

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
