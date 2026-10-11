import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { DataTable } from "@/shared/components/DataTable";
import { Pill } from "@/shared/components/Pill";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { formatDuration, formatUsd } from "@/shared/lib/money";
import { callerLabel } from "@/shared/lib/phoneDisplay";
import { searchOrderCalls } from "@/modules/orders/api/calls";
import type { CallOutcome, OrderCallResponse } from "@/shared/api/types";
import { pipelineLabel } from "@/shared/lib/pipelines";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDayMonthTime } from "@/shared/lib/dates";
import { callOutcomeLabels, translateEnum } from "@/shared/i18n/translations";

const OUTCOMES: CallOutcome[] = [
  "ResolvedByAgent",
  "EscalatedResolvedByStaff",
  "EscalatedAbandoned",
  "FailedAgentLimitation",
  "NoAnswer",
];

/** The order line's call log.
 *
 * No classification filter and no row click: an order call either produced an order or it did
 * not, and that is a column rather than a label. There is no detail page yet because nothing
 * stores a transcript for these calls — a row that opened onto an empty page would be worse
 * than one that does not open. */
export function OrderCallLogPage() {
  const { token } = useAuth();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();

  // The dashboard tiles link here with their outcome and range attached, so a number you clicked
  // opens the calls it was counting rather than the calls from some other month.
  const [params] = useSearchParams();

  const [outcome, setOutcome] = useState<CallOutcome | "">(
    () => (params.get("outcome") as CallOutcome | null) ?? "",
  );
  const [from, setFrom] = useState(() => params.get("from") ?? "");
  const [to, setTo] = useState(() => params.get("to") ?? "");

  const state = useApiData(
    () =>
      searchOrderCalls(token!, {
        outcome: outcome || undefined,
        // The end of the chosen day, not its midnight: one date in both boxes would otherwise
        // ask for a range of zero length and report an empty day back.
        from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
        to: to ? new Date(`${to}T23:59:59.999`).toISOString() : undefined,
      }),
    [token, outcome, from, to],
  );

  const rows = state.status === "success" ? state.data : [];

  // Whether this caller may be shown call costs, read off the data rather than off the session:
  // the server already decided, and a second copy of that decision on the client is a second
  // thing that can disagree with it. See CallCostVisibility.
  const showsCosts = rows.some((c) => c.costUsd !== null);

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("callLog")}
      </h1>
      {/* Every filter carries its caption, so every box sits on the same line — see the note on
          .filter-bar, where centring captioned and bare controls together put them out of line. */}
      <div className="filter-bar" style={{ marginBottom: "var(--space-4)" }}>
        <label className="filter-field">
          <span className="label">{t("colOutcome")}</span>
          <select value={outcome} onChange={(e) => setOutcome(e.target.value as CallOutcome | "")}>
          <option value="">{t("allOutcomes")}</option>
          {OUTCOMES.map((o) => (
            <option key={o} value={o}>
              {translateEnum(callOutcomeLabels, o, language)}
            </option>
          ))}
          </select>
        </label>
        <label className="filter-field">
          <span className="label">{t("dateFrom")}</span>
          <input type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label className="filter-field">
          <span className="label">{t("dateTo")}</span>
          <input type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} />
        </label>
      </div>
      {state.status === "error" && <div className="field-error">{t("failedToLoadCalls")}</div>}
      <DataTable
        loading={state.status === "loading"}
        rows={rows}
        rowKey={(c) => c.id}
        emptyMessage={outcome ? t("noCallsMatchFilters") : t("noCallsYet")}
        columns={[
          { header: t("colDateTime"), render: (c) => formatDayMonthTime(c.startedAt, language), className: "mono" },
          // The name when the agent got that far, the raw caller identifier when it did not.
          // Which of the two is showing is itself the answer to "did identification work".
          { header: t("colCustomer"), render: (c) => callerLabel(c.customerName, c.callerPhoneNumber, t("unknownCaller")) },
          {
            header: t("colOrder"),
            render: (c) => (c.relatedOrderId === null ? "—" : `#${c.relatedOrderId}`),
            className: "mono",
          },
          {
            header: t("colOutcome"),
            render: (c) => (
              <Pill variant={c.resolvedByAgent ? "success" : "critical"}>
                {translateEnum(callOutcomeLabels, c.outcome, language)}
              </Pill>
            ),
          },
          { header: t("colDurationShort"), render: (c) => formatDuration(c.durationSeconds), className: "mono" },
          // Questions over answers, side by side: a high answer count on its own reads as a
          // thorough call, when it can just as easily mean five turns to handle one question.
          {
            header: t("colQuestionsAnswers"),
            render: (c) => `${c.callerTurnCount}/${c.turnCount}`,
            className: "mono",
          },
          // The mode, with the exact model behind it on hover — the rate card is per model, and
          // a model swap is when a jump in the cost column needs explaining.
          //
          // ⚠ Withheld with the costs. Naming the provider hands over half of what the flag
          // exists to withhold, because the provider publishes the other half.
          ...(showsCosts
            ? [
                {
                  header: t("colAgent"),
                  render: (c: OrderCallResponse) => (
                    <span title={c.agentModel ?? undefined}>{pipelineLabel(c.pipeline)}</span>
                  ),
                },
              ]
            : []),
          // See CallCostVisibility — absent means withheld, and a column of dashes reads as
          // data that failed to load.
          ...(showsCosts
            ? [
                {
                  header: t("colCost"),
                  render: (c: OrderCallResponse) => formatUsd(c.costUsd),
                  className: "mono",
                },
                {
                  header: t("colCostPerMinute"),
                  render: (c: OrderCallResponse) => formatUsd(c.costPerMinuteUsd),
                  className: "mono",
                },
              ]
            : []),
        ]}
      />
    </AppShell>
  );
}
