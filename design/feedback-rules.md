# Feedback module — rules

Agreed 16 Aug 2026. Bullets only. This is the specification; where the code disagrees, the code is
wrong.

## 1. How the agent acts

- Knows who it is calling **before** it dials — the request is queued first, with a name and a number.
- Greets by name. Says once how many questions there are.
- Asks the questions in order. Never skips ahead, never goes back.
- One question per turn.
  - **Yes/No** and **Scale** — the options are **not** read out. "1-dən 5-ə qədər" is the question.
  - **Choice** — the options are read out.
  - **Open** — no options to read.
- Every question silently accepts **"cavab vermək istəmirəm"**. Never offered aloud, always accepted.
- The caller's words are matched to an option **in code**, never by the model.

### When it does not understand

- Prefer-not-to-answer → record the decline, next question, no argument.
- Misunderstood → **ask once more**. Not twice, not five times.
- Still stuck → apologise, say a person will call them back, end the call.
  - The call is marked **Needs human**. It is never auto-redialled.
- Never invents an answer, never says a score, never reveals a tool or an id.

## 2. Question formats

Four types. Nothing numeric is ever typed by the owner.

| Type | Owner sets | Read aloud | Scores |
|---|---|---|---|
| **Yes/No** | which answer is the good one | no | 100% / 0% |
| **Scale** | top of the scale — 3, 5 or 10 | no | `(i-1)/(N-1)` → 1 is 0%, N is 100% |
| **Choice** | option labels, 2+ · tick "allow Other" | yes | none — counts only |
| **Open** | nothing | n/a | none — never charted |

- **Score is a percentage, derived, never entered.** The old `Value` box is gone, and so is the
  "number the options" tick that put `Bəli=1, Xeyr=2` into `Toyota2`.
- Scale runs 1..N and 1 is the worst, so 1 → 0% and N → 100%. Consistent with Yes/No, and a
  questionnaire everybody hates reads as 0% rather than 20%.
- **"Other"** on a Choice question is an option that also carries the caller's own words. It has
  no score, like every other Choice option.
- **Counts toward the score** — a tick, on any number of Yes/No or Scale questions.
  - Replaces the old single "headline" tick.
  - Exists because "Məmnun qaldınız?" is satisfaction and "Servis kitabçası verildi mi?" is a
    fact. Averaging them makes a number that moves for two unrelated reasons.

### New-question form

- Always: **question text**, **type**, **counts toward the score** (Yes/No and Scale only).
- Yes/No: which answer is positive.
- Scale: 3, 5 or 10.
- Choice: the labels, and whether to allow "Other".
- Open: nothing further.

## 3. How we record, and how we follow up

- A **request** is a person to be surveyed. An **attempt** is one dial. One request, many attempts.
  - The request holds the person, the questionnaire, the attempt count and the outcome.
  - Each attempt holds its own duration, tokens and cost.
  - This is why 15 Aug showed "5 calls": four dead attempts and one real one, all as peers.
- Request outcomes — there is **no partial**:
  - **Not reached** — never connected, or connected with no turns. Not a survey. Retryable.
  - **Refused** — reached, would not take part. Not retried.
  - **Needs human** — broke down mid-survey. Not retried; a person rings them.
  - **Complete** — every question was put to them. Declining one still counts as put.
- **Only Complete contributes answers to the dashboard.** Anything else is a to-do, not a result.
- **Retry** — two settings per questionnaire: **delay** and **count**. Not reached only.
  - ⚠ Nothing dials today. Attempts are counted and rows marked due, but the retry itself waits
    for telephony. Same shape either way.
- Open answers come from the **caller's transcript**, not from what the model passes.
  - ⚠ The transcript is `null` for every call in every module today
    (`LiveVoiceCallOrchestrator.cs:306`). Fixing that is a prerequisite, in its own commit.
- Phone numbers are **masked everywhere in the UI** — `+994(00)000-00-00`. Stored in full.
- The person stays a standalone name and number. No link to `ord.Customer` or `app.Client`.

## 4. Dashboard

Per questionnaire, and **about the customers, not the machine**. Cost, duration, tokens and
attempts live on the Calls page.

1. **Score** — one big %, the mean of every answer to a question ticked "counts toward the score".
   - Trend against the previous period.
   - *n* completed surveys stated underneath. A 92% from four people is not 92%.
2. **Coverage** — requested · completed · response rate. The only process number here, because it
   qualifies the score above it.
3. **Per question**, one card each:
   - Yes/No → two-slice pie + % positive.
   - Choice → pie + counts.
   - Scale → distribution **bar**, not a pie. Five similar wedges cannot be compared by eye, and
     the shape of the distribution is the interesting part.
   - Open → a count and a link. Never charted, never summarised.
4. **The table** — every question × every option, count and %. The thing you would export.
5. Filters: questionnaire (required), date range.

**Removed**: "Harada dayanırlar". It mixed unreachable people into a rate about question wording.

## 5. Other

- Gemini only. One instruction file, written for that model. The server refuses other pipelines.
- Options that already have answers are frozen — editing them would orphan reported numbers.
- Quota: `MaxSurveys` per tenant, set by the platform admin, enforced in the service.
- Cost is written per attempt, at the rates in force then, never recalculated.
- Existing questionnaires are **converted, not wiped**: all-numeric options → Scale, `Bəli/Xeyr`
  → Yes/No with Bəli positive, anything else → Choice.
