namespace Secretary.Domain.Enums;

/// <summary>What kind of answer a question expects.
///
/// ⚠ This started as two types — Open and Choice — with a rating scale modelled as a Choice whose
/// options happened to carry numbers. It was one type too few, and the collapse hid three
/// different things inside one editable number:
///
/// - what the caller says ("4"),
/// - what the dashboard averages,
/// - and, once options were auto-numbered, their position in the list.
///
/// A real questionnaire came out of it scoring Bəli=1 and Xeyr=2, so "no" beat "yes". The types
/// are separate now and nothing numeric is typed by the owner: each type derives its own score.</summary>
public enum FeedbackQuestionType
{
    /// <summary>Answered in the caller's own words, recorded from the transcript. Never charted —
    /// the words are read on the call page.</summary>
    Open = 0,

    /// <summary>A list of named options the agent reads out. Counted, never scored: "Təmir" and
    /// "Satış" have no order between them, so an average over them would be a number about
    /// nothing. May allow an "Other" option that also captures what the caller said.</summary>
    Choice = 1,

    /// <summary>Bəli or Xeyr. The options are not read out — the question already implies them —
    /// and the owner says which answer is the good one, because "Gözləmə uzun oldu?" is a
    /// question whose good answer is no.</summary>
    YesNo = 2,

    /// <summary>1 to N, where N is 3, 5 or 10. The options are not read out; "birdən beşə qədər"
    /// is the question. 1 is the worst and scores 0%, N is the best and scores 100%.</summary>
    Scale = 3,
}

/// <summary>Where a feedback call got to.
///
/// Only Completed contributes answers to the dashboard. Everything else is a to-do — somebody to
/// ring again, or somebody a person needs to ring — and the difference between those two is the
/// whole of the follow-up list.</summary>
public enum FeedbackCallStatus
{
    /// <summary>Queued by the form, not yet dialled.</summary>
    Created = 0,

    /// <summary>On the line now.</summary>
    InProgress = 1,

    /// <summary>Every question was put to them — including any they declined to answer.</summary>
    Completed = 2,

    /// <summary>The call ended before the last question. Nobody knows why, so it is retryable.</summary>
    Abandoned = 3,

    /// <summary>The survey broke down: the caller answered, and twice running the answer could not
    /// be understood.
    ///
    /// Never retried automatically. Ringing again with the same agent would fail the same way, and
    /// a second identical call is how a survey becomes a nuisance. A person rings them instead.</summary>
    NeedsHuman = 4,
}
