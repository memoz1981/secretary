namespace Secretary.Agents;

/// <summary>What the caller has actually said, for the length of one call.
///
/// ⚠ Exists because a model asked to pass on somebody's words will pass on its own. On a real
/// survey call the caller said "Ömür əllərim belə çox gözləmədim. O yaxşı idi…" and the answer
/// stored against them was "Ümumi rəylərim. Belə, çox gözləmədim. O yaxşı idi…" — the opening
/// rewritten into what the model expected to hear, in the one field whose entire purpose is the
/// caller's own words.
///
/// No instruction fixes that. "Pass on exactly what they said" was already there, in bold. The
/// model is not lying; it is doing what models do with text, which is tidy it. So the words are
/// taken from the transcription the provider sends us, and the model's version of them is a
/// fallback for when there is no transcription at all.
///
/// The same principle as matching options in code rather than letting the model choose: anything
/// the caller said is evidence, and evidence does not go through a paraphraser.</summary>
public sealed class CallerSpeech
{
    private readonly List<string> _said = [];

    /// <summary>How much had been said when the current question was put. Everything after this
    /// is the answer to it.</summary>
    private int _mark;

    /// <summary>Written by the orchestrator as transcriptions arrive.</summary>
    public void Heard(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            _said.Add(text.Trim());
        }
    }

    /// <summary>A question has just been asked; the answer starts here.</summary>
    public void StartOfAnswer() => _mark = _said.Count;

    /// <summary>Everything the caller has said since the question was put, joined.
    ///
    /// Several fragments because a pause ends a turn without ending a sentence — "…çox
    /// gözləmədim" and "…uzun müddət gözləmədim" arrive separately and are one answer. Null when
    /// they have said nothing since, which is the caller-transcription-failed case and the reason
    /// there is a fallback at all.</summary>
    public string? SinceQuestion()
    {
        if (_said.Count <= _mark)
        {
            return null;
        }

        return string.Join(" ", _said.Skip(_mark));
    }
}
