import { useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { StatTile } from "@/shared/components/StatTile";
import { BarRow } from "@/shared/components/BarRow";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { formatTokens, formatUsd } from "@/shared/lib/money";
import { pipelineLabel } from "@/shared/lib/pipelines";
import { CALL_PIPELINES } from "@/shared/api/types";
import { getDashboardSummary } from "@/shared/api/dashboard";
import { useLanguage } from "@/shared/i18n/LanguageContext";

type RangeOption = "today" | "week" | "month";

function rangeToDates(range: RangeOption): { from: string; to: string } {
  const to = new Date();
  const from = new Date();
  if (range === "today") from.setHours(0, 0, 0, 0);
  if (range === "week") from.setDate(from.getDate() - 7);
  if (range === "month") from.setMonth(from.getMonth() - 1);
  return { from: from.toISOString(), to: to.toISOString() };
}

export function DashboardPage() {
  const { token } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const [range, setRange] = useState<RangeOption>("week");
  const { from, to } = rangeToDates(range);
  const state = useApiData(() => getDashboardSummary(token!, from, to), [token, range]);

  const pct = (n: number) => `${Math.round(n * 100)}%`;
  const fmtSeconds = (s: number) => `${Math.floor(s / 60)}m ${Math.round(s % 60)}s`;

  // Every mode gets a row whether or not it was dialled in this range: an absent row reads as
  // "no data available", when the useful fact is "this one has not been tried yet". Unused
  // rows show dashes rather than $0.00, which would read as free. Calls logged before mode
  // tracking existed (Unknown) have no row — they still count in the totals above, where they
  // contribute the $0 they genuinely cost.
  const spendRows = CALL_PIPELINES.map((pipeline) => ({
    pipeline,
    data: state.status === "success" ? state.data.spendByPipeline.find((row) => row.pipeline === pipeline) : undefined,
  }));
  function rangeLabel(r: RangeOption) {
    if (r === "today") return t("today");
    if (r === "week") return t("rangeWeek");
    return t("rangeMonth");
  }

  return (
    <AppShell {...shell}>
      <div className="toolbar">
        <h1 className="page-title">{t("kpiDashboard")}</h1>
        <div className="view-toggle">
          {(["today", "week", "month"] as RangeOption[]).map((r) => (
            <span key={r} className={range === r ? "active" : ""} onClick={() => setRange(r)}>
              {rangeLabel(r)}
            </span>
          ))}
        </div>
      </div>

      {state.status === "error" && <div className="field-error">{t("failedToLoadDashboard")}</div>}
      {state.status === "success" && (
        <>
          <div className="stat-grid">
            <StatTile label={t("totalCalls")} value={String(state.data.totalCalls)} />
            <StatTile
              label={t("resolvedByAgent")}
              value={pct(state.data.resolvedByAgentRate)}
              sub={`${state.data.resolvedByAgentCount} ${t("callsSuffix")}`}
            />
            <StatTile label={t("escalationRate")} value={pct(state.data.escalationRate)} sub={`${state.data.escalationCount} ${t("callsSuffix")}`} />
            <StatTile
              label={t("avgCallDuration")}
              value={fmtSeconds(state.data.averageCallDurationSeconds)}
              sub={`${state.data.averageTurnsToResolution.toFixed(1)} ${t("avgTurnsSuffix")}`}
            />
          </div>
          {/* What the agent cost to run over this range. Sits directly under the volume tiles
              because the two only mean anything together: 400 calls is good news or bad
              depending entirely on the line below it. */}
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
          {/* Per mode, because a blended average across architectures that differ tenfold in
              cost describes none of them. All four are listed whether or not they were dialled
              in this range — the gaps are part of the comparison. */}
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
                      <td className="mono">{data ? fmtSeconds(data.averageCallDurationSeconds) : "—"}</td>
                      <td className="mono">{data?.models || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
          <div className="row" style={{ display: "flex", gap: "var(--space-4)", marginTop: "var(--space-4)" }}>
            <Card style={{ flex: 1 }}>
              <h2>{t("escalationOutcomes")}</h2>
              <BarRow label={t("resolvedByStaff")} pct={state.data.escalationResolvedByStaffRate} color="var(--color-success)" />
              <BarRow label={t("abandonedNoPickup")} pct={state.data.escalationAbandonedRate} color="var(--color-critical)" />
            </Card>
            <Card style={{ flex: 1 }}>
              <h2>{t("failureBreakdown")}</h2>
              <BarRow label={t("noCalendarAvailability")} pct={state.data.failedNoAvailabilityRate} color="var(--color-warning)" />
              <BarRow label={t("agentLimitation")} pct={state.data.failedAgentLimitationRate} color="var(--color-critical)" />
            </Card>
          </div>
          <div className="row" style={{ display: "flex", gap: "var(--space-4)", marginTop: "var(--space-4)" }}>
            <Card style={{ flex: 1 }}>
              <h2>{t("appointmentVolume")}</h2>
              <div className="stat" style={{ border: "none", padding: 0, background: "none" }}>
                <div className="value">{state.data.appointmentVolume}</div>
                <div className="sub">{t("bookedInThisRange")}</div>
              </div>
            </Card>
            <Card style={{ flex: 1 }}>
              <h2>{t("reminderNoAnswerRate")}</h2>
              <div className="stat" style={{ border: "none", padding: 0, background: "none" }}>
                <div className="value">{pct(state.data.reminderNoAnswerRate)}</div>
                <div className="sub">{t("ofReminderCallsInRange")}</div>
              </div>
            </Card>
          </div>
        </>
      )}
    </AppShell>
  );
}
