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
