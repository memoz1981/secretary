import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { DataTable } from "@/shared/components/DataTable";
import { Pill } from "@/shared/components/Pill";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { formatDuration, formatUsd } from "@/shared/lib/money";
import { searchCalls } from "@/modules/appointments/api/calls";
import { CALL_PIPELINES, type CallClassification, type CallOutcome, type CallPipeline } from "@/shared/api/types";
import { PIPELINE_INFO, pipelineLabel } from "@/shared/lib/pipelines";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDayMonthTime } from "@/shared/lib/dates";
import { callClassificationLabels, callOutcomeLabels, translateEnum } from "@/shared/i18n/translations";

const CLASSIFICATIONS: CallClassification[] = [
  "NewAppointment",
  "UpdateReschedule",
  "Cancellation",
  "ReminderConfirmation",
  "InquiryOther",
];
const OUTCOMES: CallOutcome[] = [
  "ResolvedByAgent",
  "EscalatedResolvedByStaff",
  "EscalatedAbandoned",
  "FailedNoAvailability",
  "FailedAgentLimitation",
  "NoAnswer",
];

function outcomePillVariant(outcome: CallOutcome): "success" | "warning" | "critical" {
  if (outcome === "ResolvedByAgent") return "success";
  if (outcome === "EscalatedResolvedByStaff") return "warning";
  return "critical";
}

export function CallLogPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();
  const [classification, setClassification] = useState<CallClassification | "">("");
  const [outcome, setOutcome] = useState<CallOutcome | "">("");
  const [pipeline, setPipeline] = useState<CallPipeline | "">("");

  const state = useApiData(
    () =>
      searchCalls(token!, {
        classification: classification || undefined,
        outcome: outcome || undefined,
        pipeline: pipeline || undefined,
      }),
    [token, classification, outcome, pipeline],
  );

  // Calls logged before mode tracking existed carry no mode, and with four architectures in
  // one log every other figure on the row is unreadable without knowing which produced it.
  // Hidden rather than shown as a row of dashes.
  const rows = state.status === "success" ? state.data.filter((c) => c.pipeline !== "Unknown") : [];

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("callLog")}
      </h1>
      <div className="filters" style={{ display: "flex", gap: "var(--space-3)", marginBottom: "var(--space-4)" }}>
        <select value={classification} onChange={(e) => setClassification(e.target.value as CallClassification | "")}>
          <option value="">{t("allClassifications")}</option>
          {CLASSIFICATIONS.map((c) => (
            <option key={c} value={c}>
              {translateEnum(callClassificationLabels, c, language)}
            </option>
          ))}
        </select>
        <select value={outcome} onChange={(e) => setOutcome(e.target.value as CallOutcome | "")}>
          <option value="">{t("allOutcomes")}</option>
          {OUTCOMES.map((o) => (
            <option key={o} value={o}>
              {translateEnum(callOutcomeLabels, o, language)}
            </option>
          ))}
        </select>
        {/* The filter that makes the four comparable: one architecture at a time, same range. */}
        <select value={pipeline} onChange={(e) => setPipeline(e.target.value as CallPipeline | "")}>
          <option value="">{t("allPipelines")}</option>
          {CALL_PIPELINES.map((p) => (
            <option key={p} value={p}>
              {PIPELINE_INFO[p].short}
            </option>
          ))}
        </select>
      </div>
      {state.status === "error" && <div className="field-error">{t("failedToLoadCalls")}</div>}
      <DataTable
        loading={state.status === "loading"}
        rows={rows}
        rowKey={(c) => c.id}
        onRowClick={(c) => navigate(`/appointments/calls/${c.id}`)}
        emptyMessage={classification || outcome || pipeline ? t("noCallsMatchFilters") : t("noCallsYet")}
        columns={[
          { header: t("colDateTime"), render: (c) => formatDayMonthTime(c.startedAt, language), className: "mono" },
          // Immediately after the timestamp: with four pipelines in one log, every other column
          // on the row is meaningless until you know which one produced it.
          { header: t("colPipeline"), render: (c) => pipelineLabel(c.pipeline) },
          { header: t("colClient"), render: (c) => c.clientName ?? c.callerPhoneNumber },
          { header: t("colClassification"), render: (c) => translateEnum(callClassificationLabels, c.classification, language) },
          {
            header: t("colOutcome"),
            render: (c) => (
              <Pill variant={outcomePillVariant(c.outcome)}>{translateEnum(callOutcomeLabels, c.outcome, language)}</Pill>
            ),
          },
          {
            header: t("colDurationShort"),
            render: (c) => formatDuration(c.durationSeconds),
            className: "mono",
          },
          // Questions over answers, side by side: on its own, a high answer count reads as a
          // thorough call, when it can just as easily mean the agent needed five turns to
          // handle one question.
          {
            header: t("colQuestionsAnswers"),
            render: (c) => `${c.callerTurnCount}/${c.turnCount}`,
            className: "mono",
          },
          { header: t("colCost"), render: (c) => formatUsd(c.costUsd), className: "mono" },
          { header: t("colCostPerMinute"), render: (c) => formatUsd(c.costPerMinuteUsd), className: "mono" },
        ]}
      />
      <div className="note">{t("rowClickToCallDetail")}</div>
    </AppShell>
  );
}
