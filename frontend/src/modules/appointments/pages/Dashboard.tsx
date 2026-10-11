import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { StatTile } from "@/shared/components/StatTile";
import { OutcomeTile } from "@/shared/components/OutcomeTile";
import { OUTCOME_TONE } from "@/modules/appointments/lib/outcomes";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { formatDuration, formatTokens, formatUsd } from "@/shared/lib/money";
import { pipelineLabel } from "@/shared/lib/pipelines";
import { APPOINTMENT_CALL_OUTCOMES, CALL_PIPELINES, type AppointmentCallOutcome } from "@/shared/api/types";
import { getDashboardSummary } from "@/modules/appointments/api/dashboard";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { appointmentCallOutcomeLabels, translateEnum } from "@/shared/i18n/translations";

/** yyyy-MM-dd, which is what <input type="date"> speaks. */
function isoDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function daysAgo(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return isoDate(date);
}

export function DashboardPage() {
  const { token } = useAuth();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();
  const navigate = useNavigate();

  // ⚠ Two dates rather than today/week/month. Those three answered whichever question they were
  // built for and nothing else — "how did last Tuesday go" and "what did the campaign week cost"
  // were both unaskable — and the buttons read as disabled controls besides.
  const [from, setFrom] = useState(() => daysAgo(7));
  const [to, setTo] = useState(() => isoDate(new Date()));

  // The end of the chosen day, not its midnight. Picking the same date for both otherwise asks
  // for a range of zero length and reports an empty day back at you.
  const state = useApiData(
    () => getDashboardSummary(token!, new Date(`${from}T00:00:00`).toISOString(), new Date(`${to}T23:59:59.999`).toISOString()),
    [token, from, to],
    { keepPrevious: true },
  );

  const spendRows = CALL_PIPELINES.map((pipeline) => ({
    pipeline,
    data: state.status === "success" ? state.data.spendByPipeline.find((row) => row.pipeline === pipeline) : undefined,
  }));

  /** Straight through to the calls that make up a tile, with the same range applied. A number
   *  you cannot open is a number you have to take on trust. */
  function openCalls(outcome?: AppointmentCallOutcome) {
    const query = new URLSearchParams({ from, to });
    if (outcome) query.set("outcome", outcome);
    navigate(`/appointments/calls?${query.toString()}`);
  }

  return (
    <AppShell {...shell}>
      <div className="toolbar">
        <h1 className="page-title">{t("navDashboard")}</h1>
        <div className="filter-bar">
          <label className="filter-field">
            <span className="label">{t("dateFrom")}</span>
            <input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label className="filter-field">
            <span className="label">{t("dateTo")}</span>
            <input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
          </label>
        </div>
      </div>

      {state.status === "error" && <div className="field-error">{t("failedToLoadDashboard")}</div>}
      {state.status === "success" && (
        <>
          <div className="outcome-grid">
            <OutcomeTile
              label={t("totalCalls")}
              count={state.data.totalCalls}
              tone="total"
              onClick={() => openCalls()}
            />
            {/* In the declared order, not the order the server happened to return. The tiles keep
                their positions between ranges, which is what lets somebody read the page at a
                glance after the first week. */}
            {APPOINTMENT_CALL_OUTCOMES.map((outcome) => {
              const row = state.data.byOutcome.find((o) => o.outcome === outcome);
              return (
                <OutcomeTile
                  key={outcome}
                  label={translateEnum(appointmentCallOutcomeLabels, outcome, language)}
                  count={row?.calls ?? 0}
                  share={row?.share ?? 0}
                  tone={OUTCOME_TONE[outcome]}
                  onClick={() => openCalls(outcome)}
                />
              );
            })}
          </div>

          <div className="stat-grid" style={{ marginTop: "var(--space-4)" }}>
            <StatTile label={t("totalCallMinutes")} value={formatDuration(state.data.totalDurationSeconds)} />
            <StatTile
              label={t("avgCallDuration")}
              value={formatDuration(Math.round(state.data.averageCallDurationSeconds))}
            />
            {/* Over resolved calls only — see the server. Dashes rather than 0m when nothing
                resolved: zero would read as a line that finishes everything instantly. */}
            <StatTile
              label={t("avgResolvedCallDuration")}
              value={
                state.data.averageResolvedCallDurationSeconds === null
                  ? "—"
                  : formatDuration(Math.round(state.data.averageResolvedCallDurationSeconds))
              }
            />
            <StatTile label={t("appointmentsMade")} value={String(state.data.appointmentsMade)} />
          </div>

          {/* What the agent cost to run over this range. Sits directly under the volume tiles
              because the two only mean anything together: 400 calls is good news or bad
              depending entirely on the line below it.

              Both money cards are absent for a tenant who may not see costs — see
              CallCostVisibility. Not greyed out and not zeroed: a card of dashes invites the
              question, and the answer is not one we want to be having on a customer's screen. */}
          {state.data.totalCostUsd !== null && (
          <Card style={{ marginTop: "var(--space-4)" }}>
            <h2>{t("agentSpend")}</h2>
            <div className="stat-grid" style={{ marginTop: "var(--space-3)" }}>
              <StatTile
                label={t("totalSpend")}
                value={formatUsd(state.data.totalCostUsd)}
                sub={`${formatTokens(state.data.totalTokens)} ${t("tokensSuffix")}`}
              />
              <StatTile
                label={t("avgCostPerCall")}
                value={formatUsd(state.data.averageCostPerCallUsd)}
                sub={`${state.data.totalCalls} ${t("callsSuffix")}`}
              />
              <StatTile
                label={t("costPerMinute")}
                value={formatUsd(state.data.averageCostPerMinuteUsd)}
                sub={t("perMinuteSuffix")}
              />
              <StatTile
                label={t("costPerAnswer")}
                value={formatUsd(state.data.averageCostPerAnswerUsd)}
                sub={t("perAnswerSuffix")}
              />
            </div>
          </Card>
          )}

          {/* Per mode, because a blended average across architectures that differ tenfold in
              cost describes none of them. */}
          {state.data.spendByPipeline.length > 0 && CALL_PIPELINES.length > 1 && (
          <Card style={{ marginTop: "var(--space-4)" }}>
            <h2>{t("spendByPipeline")}</h2>
            <div className="table-scroll">
              <table className="data striped" style={{ marginTop: "var(--space-3)" }}>
                <thead>
                  <tr>
                    <th>{t("colPipeline")}</th>
                    <th className="mono">{t("colCalls")}</th>
                    <th className="mono">{t("totalSpend")}</th>
                    <th className="mono">{t("avgCostPerCall")}</th>
                    <th className="mono">{t("costPerMinute")}</th>
                    <th className="mono">{t("colAvgDuration")}</th>
                    <th>{t("colModels")}</th>
                  </tr>
                </thead>
                <tbody>
                  {spendRows.map(({ pipeline, data }) => (
                    <tr key={pipeline} className={data ? "" : "unused"}>
                      <td style={{ whiteSpace: "nowrap" }}>{pipelineLabel(pipeline)}</td>
                      <td className="mono">{data ? data.calls : 0}</td>
                      <td className="mono">{data ? formatUsd(data.totalCostUsd) : "—"}</td>
                      <td className="mono">{data ? formatUsd(data.averageCostPerCallUsd) : "—"}</td>
                      <td className="mono">{data ? formatUsd(data.averageCostPerMinuteUsd) : "—"}</td>
                      <td className="mono">{data ? formatDuration(Math.round(data.averageCallDurationSeconds)) : "—"}</td>
                      <td className="mono">{data?.models || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
          )}
        </>
      )}
    </AppShell>
  );
}
