import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { DataTable } from "@/shared/components/DataTable";
import { Pill } from "@/shared/components/Pill";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDayMonthTime } from "@/shared/lib/dates";
import { formatDuration, formatUsd } from "@/shared/lib/money";
import { getSurveys, searchFeedbackCalls } from "@/modules/feedback/api/feedback";
import { maskAzPhone, revealAzPhone } from "@/modules/feedback/api/phone";
import type { FeedbackCallStatus } from "@/shared/api/types";

/** Every status a finished or in-flight dial can be in, in the order they happen. */
const CALL_STATUSES: FeedbackCallStatus[] = [
  "Created",
  "InProgress",
  "Completed",
  "Abandoned",
  "NeedsHuman",
];

function statusVariant(status: FeedbackCallStatus): "success" | "warning" | "critical" | "neutral" {
  if (status === "Completed") return "success";
  // Both need somebody to act, and both are red for that reason — but they are not the same
  // action. Abandoned gets dialled again; NeedsHuman gets a person, because the same agent
  // ringing back would fail the same way.
  if (status === "Abandoned" || status === "NeedsHuman") return "critical";
  if (status === "InProgress") return "warning";
  return "neutral";
}

/** Every survey call, newest first. Its own page rather than a panel on the dashboard: the
 *  dashboard answers "what do customers think", this answers "what happened to this person". */
export function FeedbackCallsPage() {
  const { token } = useAuth();
  const { t, language } = useLanguage();
  const shell = useBusinessShell();
  const navigate = useNavigate();
  const [surveyId, setSurveyId] = useState("");
  const [status, setStatus] = useState<FeedbackCallStatus | "">("");

  // One switch for the page, not one per row. Somebody who needs a number usually needs to scan
  // several, and clicking each in turn is a reveal that pretends to be a decision.
  const [numbersShown, setNumbersShown] = useState(false);

  const surveys = useApiData(() => getSurveys(token!), [token]);
  const state = useApiData(
    () => searchFeedbackCalls(token!, { surveyId: surveyId === "" ? undefined : Number(surveyId) }),
    [token, surveyId],
  );

  // Filtered here rather than in the query: the page already holds every call for the chosen
  // questionnaire, and a round trip to hide four rows is a round trip for nothing.
  const all = state.status === "success" ? state.data : [];
  const rows = status === "" ? all : all.filter((c) => c.status === status);

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("feedbackCalls")}
      </h1>

      <div className="filters" style={{ display: "flex", gap: "var(--space-3)", marginBottom: "var(--space-4)" }}>
        <select value={surveyId} onChange={(e) => setSurveyId(e.target.value)}>
          <option value="">{t("allQuestionnaires")}</option>
          {surveys.status === "success" &&
            surveys.data.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
        </select>
        <select value={status} onChange={(e) => setStatus(e.target.value as FeedbackCallStatus | "")}>
          <option value="">{t("allStatuses")}</option>
          {CALL_STATUSES.map((value) => (
            <option key={value} value={value}>
              {t(`feedbackStatus${value}`)}
            </option>
          ))}
        </select>
        <label className="checkbox-row" style={{ margin: 0 }}>
          <input
            type="checkbox"
            checked={numbersShown}
            onChange={(e) => setNumbersShown(e.target.checked)}
          />
          <span>{t("showNumbers")}</span>
        </label>
      </div>

      <DataTable
        loading={state.status === "loading"}
        rows={rows}
        rowKey={(c) => c.id}
        onRowClick={(c) => navigate(`/feedback/calls/${c.id}`)}
        emptyMessage={t("noFeedbackCallsYet")}
        columns={[
          { header: t("colDateTime"), render: (c) => formatDayMonthTime(c.createdAt, language), className: "mono" },
          { header: t("customerName"), render: (c) => c.personName },
          {
            // Masked until asked for, the same as the follow-up list. A column of
            // +994(00)000-00-00 is not privacy, it is a column of nothing — somebody looking at
            // a call usually wants to be able to ring the person back about it.
            header: t("colPhone"),
            render: (c) => (numbersShown ? revealAzPhone(c.phoneNumber) : maskAzPhone(c.phoneNumber)),
            className: "mono",
          },
          { header: t("questionnaire"), render: (c) => c.surveyName },
          // ⚠ This column is why the split happened. Five rows here on 15 August were one
          // person and one survey; without the attempt number they read as five surveys.
          { header: t("attempt"), render: (c) => String(c.attemptNumber), className: "mono" },
          {
            header: t("colStatus"),
            render: (c) => <Pill variant={statusVariant(c.status)}>{t(`feedbackStatus${c.status}`)}</Pill>,
          },
          // Answered out of asked, which is the one number that says whether the call worked.
          {
            header: t("colAnswered"),
            render: (c) => `${c.answeredCount}/${c.questionCount}`,
            className: "mono",
          },
          { header: t("colDurationShort"), render: (c) => formatDuration(c.durationSeconds), className: "mono" },
          { header: t("colCost"), render: (c) => formatUsd(c.costUsd), className: "mono" },
        ]}
      />
      <div className="note">{t("rowClickToCallDetail")}</div>
    </AppShell>
  );
}
