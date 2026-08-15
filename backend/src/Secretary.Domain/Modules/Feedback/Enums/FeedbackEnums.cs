namespace Secretary.Domain.Enums;

/// <summary>What kind of answer a question expects.
///
/// Two kinds, not three. A rating scale looks like a third — "1 to 5" — but it is a choice whose
/// options happen to carry numbers, and modelling it separately would mean two code paths that
/// ask the same question and store the same answer. The number lives on the option instead, so
/// "1–5" and "Yaxşı / Pis" are one type and only one of them can be averaged.</summary>
public enum FeedbackQuestionType
{
    /// <summary>Answered in the caller's own words, recorded from the transcript. Cannot be
    /// charted — the dashboard shows these verbatim.</summary>
    Open = 0,

    /// <summary>Answered by picking one of the options. Counted always, averaged when the
    /// options carry values.</summary>
    Choice = 1,
}

/// <summary>Where a feedback call got to.
///
/// The distinction that matters is Completed versus Abandoned: a survey answered to the last
/// question is worth something and one abandoned at question two is worth knowing about, which
/// is why the agent's own reliability is half of the dashboard.</summary>
public enum FeedbackCallStatus
{
    /// <summary>Queued by the form, not yet dialled.</summary>
    Created = 0,

    /// <summary>On the line now.</summary>
    InProgress = 1,

    /// <summary>Every question was put to them — including any they declined to answer.</summary>
    Completed = 2,

    /// <summary>The call ended before the last question. Where it stopped is the most useful
    /// thing on the dashboard, so the answers given up to that point are kept.</summary>
    Abandoned = 3,
}
