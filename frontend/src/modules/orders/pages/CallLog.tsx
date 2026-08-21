import { useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { DataTable } from "@/shared/components/DataTable";
import { Pill } from "@/shared/components/Pill";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { formatDuration, formatUsd } from "@/shared/lib/money";
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
  const [outcome, setOutcome] = useState<CallOutcome | "">("");

  const state = useApiData(
    () => searchOrderCalls(token!, { outcome: outcome || undefined }),
    [token, outcome],
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
      <div className="filters" style={{ display: "flex", gap: "var(--space-3)", marginBottom: "var(--space-4)" }}>
        <select value={outcome} onChange={(e) => setOutcome(e.target.value as CallOutcome | "")}>
          <option value="">{t("allOutcomes")}</option>
          {OUTCOMES.map((o) => (
            <option key={o} value={o}>
              {translateEnum(callOutcomeLabels, o, language)}
            </option>
          ))}
        </select>
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
          { header: t("colCustomer"), render: (c) => c.customerName ?? c.callerPhoneNumber },
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
          {
            header: t("colAgent"),
            render: (c) => <span title={c.agentModel}>{pipelineLabel(c.pipeline)}</span>,
          },
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
