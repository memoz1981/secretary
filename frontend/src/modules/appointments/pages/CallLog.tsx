import type { CallResponse } from "@/shared/api/types";
import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { DataTable } from "@/shared/components/DataTable";
import { Pill } from "@/shared/components/Pill";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { formatDuration, formatUsd } from "@/shared/lib/money";
import { callerLabel } from "@/shared/lib/phoneDisplay";
import { searchCalls } from "@/modules/appointments/api/calls";
import {
  APPOINTMENT_CALL_OUTCOMES,
  CALL_PIPELINES,
  type AppointmentCallOutcome,
  type CallClassification,
  type CallPipeline,
} from "@/shared/api/types";
import { PIPELINE_INFO, pipelineLabel } from "@/shared/lib/pipelines";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDayMonthTime } from "@/shared/lib/dates";
import { appointmentCallOutcomeLabels, callClassificationLabels, translateEnum } from "@/shared/i18n/translations";
import { outcomePillVariant } from "@/modules/appointments/lib/outcomes";

const CLASSIFICATIONS: CallClassification[] = [
  "NewAppointment",
  "UpdateReschedule",
  "Cancellation",
  "ReminderConfirmation",
  "InquiryOther",
];


export function CallLogPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();

  // The dashboard tiles link here with their outcome and range attached, so a number you clicked
  // opens the calls it was counting rather than the calls from some other week.
  const [params] = useSearchParams();

  const [classification, setClassification] = useState<CallClassification | "">("");
  const [outcome, setOutcome] = useState<AppointmentCallOutcome | "">(
    () => (params.get("outcome") as AppointmentCallOutcome | null) ?? "",
  );
  const [from, setFrom] = useState(() => params.get("from") ?? "");
  const [to, setTo] = useState(() => params.get("to") ?? "");
  const [pipeline, setPipeline] = useState<CallPipeline | "">("");

  const state = useApiData(
    () =>
      searchCalls(token!, {
        classification: classification || undefined,
        outcome: outcome || undefined,
        pipeline: pipeline || undefined,
        // The end of the chosen day, not its midnight: picking one date for both would
        // otherwise ask for a range of zero length and report an empty day back.
        from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
        to: to ? new Date(`${to}T23:59:59.999`).toISOString() : undefined,
      }),
    [token, classification, outcome, pipeline, from, to],
  );

  // Calls logged before mode tracking existed carry no mode, and with four architectures in
  // one log every other figure on the row is unreadable without knowing which produced it.
  // Hidden rather than shown as a row of dashes.
  const rows = state.status === "success" ? state.data.filter((c) => c.pipeline !== "Unknown") : [];

  // Whether this caller may be shown call costs, read off the data rather than off the session:
  // the server already decided, and a second copy of that decision on the client is a second
  // thing that can disagree with it. See CallCostVisibility.
  const showsCosts = rows.some((c) => c.costUsd !== null);

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("callLog")}
      </h1>
      <div className="filter-bar" style={{ marginBottom: "var(--space-4)" }}>
        <select value={classification} onChange={(e) => setClassification(e.target.value as CallClassification | "")}>
          <option value="">{t("allClassifications")}</option>
          {CLASSIFICATIONS.map((c) => (
            <option key={c} value={c}>
              {translateEnum(callClassificationLabels, c, language)}
            </option>
          ))}
        </select>
        <select value={outcome} onChange={(e) => setOutcome(e.target.value as AppointmentCallOutcome | "")}>
          <option value="">{t("allOutcomes")}</option>
          {APPOINTMENT_CALL_OUTCOMES.map((o) => (
            <option key={o} value={o}>
              {translateEnum(appointmentCallOutcomeLabels, o, language)}
            </option>
          ))}
        </select>
        {/* Handed over by the dashboard tiles along with the outcome, so a number you clicked
            opens the calls it was counting rather than the calls from some other week. */}
        <label className="field" style={{ margin: 0 }}>
          <span className="label">{t("dateFrom")}</span>
          <input type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label className="field" style={{ margin: 0 }}>
          <span className="label">{t("dateTo")}</span>
          <input type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} />
        </label>
        {/* The filter that made the architectures comparable, one at a time. Absent while there
            is only one to pick — see CALL_PIPELINES. */}
        {CALL_PIPELINES.length > 1 && (
          <select value={pipeline} onChange={(e) => setPipeline(e.target.value as CallPipeline | "")}>
            <option value="">{t("allPipelines")}</option>
            {CALL_PIPELINES.map((p) => (
              <option key={p} value={p}>
                {PIPELINE_INFO[p].short}
              </option>
            ))}
          </select>
        )}
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
          // Immediately after the timestamp, when it is shown at all: with several pipelines in
          // one log, every other column on the row is meaningless until you know which one
          // produced it.
          //
          // ⚠ Withheld with the costs. Which architecture answered names the provider, the
          // provider publishes its rates, and a tenant who has one of those can work out the
          // other — which is the number the flag exists to withhold.
          ...(showsCosts
            ? [{ header: t("colPipeline"), render: (c: CallResponse) => pipelineLabel(c.pipeline) }]
            : []),
          { header: t("colClient"), render: (c) => callerLabel(c.clientName, c.callerPhoneNumber, t("unknownCaller")) },
          { header: t("colClassification"), render: (c) => translateEnum(callClassificationLabels, c.classification, language) },
          {
            header: t("colOutcome"),
            render: (c) => (
              <Pill variant={outcomePillVariant(c.outcome)}>{translateEnum(appointmentCallOutcomeLabels, c.outcome, language)}</Pill>
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
          // Dropped entirely rather than filled with dashes when this tenant may not see
          // costs — a column of "—" reads as data we failed to load. See CallCostVisibility.
          ...(showsCosts
            ? [
                { header: t("colCost"), render: (c: CallResponse) => formatUsd(c.costUsd), className: "mono" },
                {
                  header: t("colCostPerMinute"),
                  render: (c: CallResponse) => formatUsd(c.costPerMinuteUsd),
                  className: "mono",
                },
              ]
            : []),
        ]}
      />
      <div className="note">{t("rowClickToCallDetail")}</div>
    </AppShell>
  );
}
