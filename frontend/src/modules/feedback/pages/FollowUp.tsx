import { useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { DataTable } from "@/shared/components/DataTable";
import { Pill } from "@/shared/components/Pill";
import { StatTile } from "@/shared/components/StatTile";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDayMonthTime } from "@/shared/lib/dates";
import { closeRequest, getSurveys, getSurveyRequests, retryRequest } from "@/modules/feedback/api/feedback";
import { maskAzPhone } from "@/modules/feedback/api/phone";
import type { SurveyRequestOutcome, SurveyRequestResponse } from "@/shared/api/types";

function outcomeVariant(outcome: SurveyRequestOutcome): "success" | "warning" | "critical" | "neutral" {
  if (outcome === "Complete") return "success";
  if (outcome === "NeedsHuman") return "critical";
  if (outcome === "NotReached") return "warning";
  return "neutral";
}

/** The people, not the dials — and specifically the people somebody still has to do something
 * about.
 *
 * ⚠ This page exists because of a question that had no answer: "how many people did we survey?"
 * The calls list answered "how many times did we press the button", which on 15 August was five
 * for one person. One row per person here, however many times we rang them.
 *
 * Two kinds of row need a human, and the difference decides what they do. **Not reached** is a
 * number that never answered and has run out of retries — ring it again, or give up on it.
 * **Needs a person** broke down mid-survey, and ringing back with the same agent would fail the
 * same way, so the retry button is not offered for it. A refusal is on nobody's list: it is an
 * answer, and calling back somebody who said no is how a number gets blocked. */
export function FeedbackFollowUpPage() {
  const { token } = useAuth();
  const { t, language } = useLanguage();
  const shell = useBusinessShell();

  const [surveyId, setSurveyId] = useState("");
  const [refreshKey, setRefreshKey] = useState(0);
  const [busyId, setBusyId] = useState<number | null>(null);

  const surveys = useApiData(() => getSurveys(token!), [token]);
  const state = useApiData(
    () => getSurveyRequests(token!, surveyId === "" ? {} : { surveyId: Number(surveyId) }),
    [token, surveyId, refreshKey],
  );

  const all = state.status === "success" ? state.data : [];
  const waiting = all.filter((r) => r.needsFollowUp);
  const due = all.filter((r) => r.nextAttemptDueAt !== null);

  async function act(request: SurveyRequestResponse, what: "retry" | "close") {
    setBusyId(request.id);
    try {
      if (what === "retry") {
        await retryRequest(token!, request.id);
      } else {
        await closeRequest(token!, request.id);
      }

      setRefreshKey((k) => k + 1);
    } catch {
      // The banner reports it — somebody who already answered cannot be rung again.
    } finally {
      setBusyId(null);
    }
  }

  return (
    <AppShell {...shell}>
      <h1 className="page-title">{t("followUp")}</h1>
      <div className="subtitle">{t("followUpSubtitle")}</div>

      <div className="filters" style={{ display: "flex", gap: "var(--space-3)", margin: "var(--space-4) 0" }}>
        <select value={surveyId} onChange={(e) => setSurveyId(e.target.value)}>
          <option value="">{t("allQuestionnaires")}</option>
          {surveys.status === "success" &&
            surveys.data.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
        </select>
      </div>

      <Card>
        <div className="stat-row">
          <StatTile label={t("waitingOnAPerson")} value={String(waiting.length)} />
          <StatTile label={t("dialsScheduled")} value={String(due.length)} />
          <StatTile label={t("peopleRequested")} value={String(all.length)} />
        </div>
        {/* Said rather than left to be inferred from an empty table. */}
        {due.length > 0 && <div className="note">{t("scheduledDialsWaitForTelephony")}</div>}
      </Card>

      <Card>
        <h2>{t("waitingOnAPerson")}</h2>
        <DataTable
          loading={state.status === "loading"}
          rows={waiting}
          rowKey={(r) => r.id}
          emptyMessage={t("nobodyWaiting")}
          columns={[
            { header: t("customerName"), render: (r) => r.personName },
            { header: t("colPhone"), render: (r) => maskAzPhone(r.phoneNumber), className: "mono muted" },
            { header: t("questionnaire"), render: (r) => r.surveyName },
            {
              header: t("colStatus"),
              render: (r) => <Pill variant={outcomeVariant(r.outcome)}>{t(`outcome${r.outcome}`)}</Pill>,
            },
            { header: t("attempts"), render: (r) => String(r.attemptCount), className: "mono" },
            {
              header: t("lastAttempt"),
              render: (r) => (r.lastAttemptAt === null ? "—" : formatDayMonthTime(r.lastAttemptAt, language)),
              className: "mono",
            },
            {
              header: "",
              render: (r) => (
                <span className="row-actions">
                  {/* Not offered for a breakdown: the same agent would fail the same way, and a
                      second identical call is how a survey becomes a nuisance. */}
                  {r.outcome === "NotReached" && (
                    <button className="link" disabled={busyId === r.id} onClick={() => void act(r, "retry")}>
                      {t("ringAgain")}
                    </button>
                  )}
                  <button className="link danger" disabled={busyId === r.id} onClick={() => void act(r, "close")}>
                    {t("stopChasing")}
                  </button>
                </span>
              ),
            },
          ]}
        />
      </Card>
    </AppShell>
  );
}
