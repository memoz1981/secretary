import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Pill } from "@/shared/components/Pill";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDayMonthTime, formatTimeOfDay } from "@/shared/lib/dates";
import { formatDuration, formatUsd } from "@/shared/lib/money";
import { pipelineLabel } from "@/shared/lib/pipelines";
import { getFeedbackCall } from "@/modules/feedback/api/feedback";
import { maskAzPhone, revealAzPhone } from "@/modules/feedback/api/phone";

/** One call, in full: who, when, what it cost, and every answer with the time it was given.
 *
 * A page rather than a modal. There is enough here to scroll, it is worth linking to, and the
 * appointment module's call detail is already a page — a second pattern for the same job would
 * only make the app feel assembled from parts.
 *
 * This is also the only place open answers appear. They are the caller's own words and there is
 * nothing honest to chart from them, so the dashboard leaves them alone and they are read
 * here. */
export function FeedbackCallDetailPage() {
  const { id } = useParams();
  const { token } = useAuth();
  const { t, language } = useLanguage();
  const shell = useBusinessShell();
  const navigate = useNavigate();

  const [numberShown, setNumberShown] = useState(false);

  const state = useApiData(() => getFeedbackCall(token!, Number(id)), [token, id]);

  if (state.status === "loading") {
    return (
      <AppShell {...shell}>
        <div className="note">{t("loading")}</div>
      </AppShell>
    );
  }

  if (state.status === "error") {
    return (
      <AppShell {...shell}>
        <div className="field-error">{t("failedToLoadCalls")}</div>
      </AppShell>
    );
  }

  const { call, answers, transcript } = state.data;

  return (
    <AppShell {...shell}>
      <button className="link" onClick={() => navigate("/feedback/calls")}>
        ← {t("backToCalls")}
      </button>

      <h1 className="page-title" style={{ marginTop: "var(--space-3)" }}>
        {call.personName}
      </h1>
      <div className="subtitle">
        {call.surveyName} · {formatDayMonthTime(call.createdAt, language)}
      </div>

      <Card>
        <div className="detail-grid">
          {/* The date and time of the CALL, once, at the top. Every answer used to carry its own
              full date — the same date, repeated down the page, for a conversation that lasted
              forty seconds. The answers now show only the clock time, which is the part that
              differs between them. */}
          <div>
            <div className="label">{t("colDateTime")}</div>
            <div className="value mono">{formatDayMonthTime(call.createdAt, language)}</div>
          </div>
          <div>
            <div className="label">{t("attempt")}</div>
            <div className="value mono">{call.attemptNumber}</div>
          </div>
          <div>
            <div className="label">{t("colPhone")}</div>
            {/* Masked, with a way to see it. It had no way at all, which on the one page that
                names a single person is where you are most likely to want to ring them. */}
            {numberShown ? (
              <div className="value mono">{revealAzPhone(call.phoneNumber)}</div>
            ) : (
              <div className="value mono muted">
                {maskAzPhone(call.phoneNumber)}{" "}
                <button className="link" onClick={() => setNumberShown(true)}>
                  {t("showNumber")}
                </button>
              </div>
            )}
          </div>
          <div>
            <div className="label">{t("colStatus")}</div>
            <div className="value">
              <Pill variant={call.isCompleted ? "success" : "critical"}>{t(`feedbackStatus${call.status}`)}</Pill>
            </div>
          </div>
          <div>
            <div className="label">{t("colAnswered")}</div>
            <div className="value mono">
              {call.answeredCount}/{call.questionCount}
            </div>
          </div>
          <div>
            <div className="label">{t("colDurationShort")}</div>
            <div className="value mono">{formatDuration(call.durationSeconds)}</div>
          </div>
          <div>
            <div className="label">{t("colQuestionsAnswers")}</div>
            <div className="value mono">
              {call.callerTurnCount}/{call.turnCount}
            </div>
          </div>
          {/* See CallCostVisibility — absent means withheld, not unknown. The mode goes with the
              money: naming the provider hands over half of what the flag exists to withhold,
              because the provider publishes the other half. */}
          {call.costUsd !== null && (
            <>
              <div>
                <div className="label">{t("colAgent")}</div>
                <div className="value" title={call.agentModel ?? undefined}>
                  {pipelineLabel(call.pipeline)}
                </div>
              </div>
              <div>
                <div className="label">{t("colCost")}</div>
                <div className="value mono">{formatUsd(call.costUsd)}</div>
              </div>
              <div>
                <div className="label">{t("colCostPerMinute")}</div>
                <div className="value mono">{formatUsd(call.costPerMinuteUsd)}</div>
              </div>
            </>
          )}
        </div>
      </Card>

      <Card>
        <h2>{t("answers")}</h2>
        {answers.length === 0 ? (
          <div className="note">{t("noAnswersRecorded")}</div>
        ) : (
          <ol className="answer-list">
            {answers.map((a) => (
              <li key={a.questionId} className="answer">
                <div className="answer-question">{a.questionText}</div>
                <div className="answer-value">
                  {a.declined ? (
                    // Asked and would not say — deliberately not blank. A blank would read the
                    // same as never having been asked, and they are different results.
                    <span className="muted">{t("declinedToAnswer")}</span>
                  ) : a.optionText !== null ? (
                    <span>
                      {a.optionText}
                      {a.optionScorePercent !== null && (
                        <span className="mono"> ({a.optionScorePercent}%)</span>
                      )}
                      {/* "Digər" carries both: the option so the count is right, the words so
                          the count means something. */}
                      {a.text !== null && <span className="verbatim"> — “{a.text}”</span>}
                    </span>
                  ) : (
                    <span className="verbatim">“{a.text}”</span>
                  )}
                </div>
                <div className="answer-time mono">{formatTimeOfDay(a.answeredAt)}</div>
              </li>
            ))}
          </ol>
        )}
      </Card>

      {/* ⚠ Always rendered, even empty. It used to vanish when there was nothing to show, and
          the honest reading of a missing card is "this feature does not exist" — which is what
          somebody concluded after a call that never called EndCall, so the row was never
          finished and nothing was ever written. An empty card says which of the two it is. */}
      <Card>
          <h2>{t("transcript")}</h2>
          {/* The date once, above; each line carries its own clock time. The lines used to be
              seconds-into-the-call — "[00:14]", which reads as a time of day and is not one. */}
          <div className="score-caption" style={{ marginTop: 0, marginBottom: "var(--space-3)" }}>
            {formatDayMonthTime(call.createdAt, language)}
          </div>
          {transcript ? (
            <pre className="transcript">{transcript}</pre>
          ) : (
            <div className="note">{t("noTranscriptYet")}</div>
          )}
        </Card>
    </AppShell>
  );
}
