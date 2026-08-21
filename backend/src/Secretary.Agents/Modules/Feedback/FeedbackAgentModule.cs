using Microsoft.Extensions.AI;
using Secretary.Agents.Feedback;
using Secretary.Agents.Tools;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Domain.Enums;

namespace Secretary.Agents;

/// <summary>Lamiya ringing back after a visit: one questionnaire, asked in order, answers
/// recorded as they come.
///
/// Different from the other two in one structural way. An appointment or an order call begins
/// with a stranger and spends its first turns working out who they are; this one knows before it
/// dials, because the call was queued against a name. Everything follows from that — no identity
/// tools, no confirmation ladder, and the subject arrives in the instructions rather than through
/// a tool call.
///
/// ⚠ Gemini only, enforced where the call is accepted rather than only hidden in the picker. The
/// instruction file exists for that provider alone.</summary>
public sealed class FeedbackAgentModule : IAgentModule
{
    private readonly FeedbackTools _feedbackTools;
    private readonly CallControlTools _callControlTools;
    private readonly FeedbackCallSession _session;
    private readonly FeedbackCallService _calls;

    public FeedbackAgentModule(
        FeedbackTools feedbackTools, CallControlTools callControlTools,
        FeedbackCallSession session, FeedbackCallService calls)
    {
        _feedbackTools = feedbackTools;
        _callControlTools = callControlTools;
        _session = session;
        _calls = calls;
    }

    public Module Key => Module.Feedback;

    public string InstructionName => "Feedback";

    /// <summary>Gemini alone. There is one instruction file and it is written for this model —
    /// see IAgentModule.SupportedPipelines for why there is no fallback.</summary>
    public IReadOnlyCollection<CallPipeline>? SupportedPipelines => [CallPipeline.GeminiLive_3_1];

    /// <summary>The only module that needs it. Open answers are the caller's own words, and
    /// without the transcript there is nothing to record — see IAgentModule.</summary>
    public bool RequiresCallerTranscription => true;

    /// <summary>Who is being rung and what about, in the instructions rather than behind a tool.
    ///
    /// A tool call would cost two model invocations and about a second of silence to learn a name
    /// the system already knew before dialling. Opening with "Salam Mehdi bəy" is most of what
    /// makes this sound like a real follow-up rather than a robocall, so it should not be the
    /// expensive part of the call.</summary>
    public async Task<string?> BuildCallContextAsync(CancellationToken cancellationToken)
    {
        if (_session.CallId is not { } callId)
        {
            return null;
        }

        var subject = await _calls.GetSubjectAsync(callId, cancellationToken);

        // ⚠ The first question is handed over here rather than fetched after the caller agrees.
        //
        // Twice now the agent has invented one in that gap. It greets, they say "bəli, buyurun",
        // and for the moment it takes to call GetNextQuestion it has agreement and nothing to
        // ask — so it made up "gələn zənglərimizi dəyərləndirmək üçün birdən beşə qədər necə
        // qiymətləndirərdiniz?", a question in no questionnaire, and asked it twice. The tools
        // refused the answer, which is the guard working, but the caller had still been asked
        // something nobody wrote and spent half a minute on it.
        //
        // Instructions did not stop this: "you do not know the questions" is already in them, in
        // bold. Removing the empty-handed moment does.
        var first = await _feedbackTools.FirstQuestionForGreeting();

        // ⚠ Each name is labelled with the job it does. On a real call the agent introduced
        // itself as "Toyota Servis" — the questionnaire, not the business. Two proper nouns
        // arrived in the same paragraph, one of them was who you are and the other was what you
        // are asking about, and nothing said which was which.
        return $"""
                ## This call

                - Greet **{subject.PersonName}** by name, on behalf of the business named at the
                  top of these instructions. That business's name is the only one you introduce
                  yourself with — the questionnaire is called "{subject.SurveyName}", and that is
                  what the call is about, never who you are.
                - Say once that there are **{subject.QuestionCount}** questions. That number and
                  no other.
                - {first}

                Record the answer as usual; the one after it comes back with `RECORDED`.
                """;
    }

    public IList<AITool> BuildTools() =>
    [
        AIFunctionFactory.Create(_feedbackTools.GetNextQuestion),
        AIFunctionFactory.Create(_feedbackTools.RecordAnswer),
        AIFunctionFactory.Create(_feedbackTools.SkipQuestion),
        AIFunctionFactory.Create(_feedbackTools.EscalateToHuman),
        AIFunctionFactory.Create(_callControlTools.EndCall),
    ];

    /// <summary>Updates the row the form already created, rather than writing a new one — the
    /// call record predates the call here. A call that was queued and never answered keeps its
    /// Created status and still appears on the list, which is the honest picture.</summary>
    public async Task LogCallAsync(CallLogEntry entry, CancellationToken cancellationToken)
    {
        if (_session.CallId is not { } callId)
        {
            return;
        }

        await _calls.LogAsync(
            new LogFeedbackCallRequest(
                callId, entry.DurationSeconds, entry.TurnCount, entry.CallerTurnCount,
                entry.Transcript, entry.Pipeline, entry.ModelUsages),
            cancellationToken);
    }
}
