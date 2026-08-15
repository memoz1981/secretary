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
import type { FeedbackCallStatus } from "@/shared/api/types";

function statusVariant(status: FeedbackCallStatus): "success" | "warning" | "critical" | "neutral" {
  if (status === "Completed") return "success";
  if (status === "Abandoned") return "critical";
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

  const surveys = useApiData(() => getSurveys(token!), [token]);
  const state = useApiData(
    () => searchFeedbackCalls(token!, { surveyId: surveyId === "" ? undefined : Number(surveyId) }),
    [token, surveyId],
  );

  const rows = state.status === "success" ? state.data : [];

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
          { header: t("colPhone"), render: (c) => c.phoneNumber, className: "mono" },
          { header: t("questionnaire"), render: (c) => c.surveyName },
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
