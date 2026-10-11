import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { StatTile } from "@/shared/components/StatTile";
import { OutcomeTile, type OutcomeTone } from "@/shared/components/OutcomeTile";
import { MAX_PIE_SLICES, SharePie, ShareBars, type Share } from "@/shared/components/SharePie";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDuration } from "@/shared/lib/money";
import { callOutcomeLabels, translateEnum } from "@/shared/i18n/translations";
import { getOrderDashboard } from "@/modules/orders/api/dashboard";
import type { CallOutcome } from "@/shared/api/types";

/** The outcomes this line records, in the order they are read.
 *
 * ⚠ The shared CallOutcome, not the appointment module's — order calls record the one the
 * orchestrator produces. NoAvailability has no meaning here (an order line has no calendar) and
 * never occurs, so it is not listed; every member that can occur is, whether or not it did. */
const OUTCOMES: { outcome: CallOutcome; tone: OutcomeTone }[] = [
  { outcome: "ResolvedByAgent", tone: "good" },
  { outcome: "EscalatedResolvedByStaff", tone: "warn" },
  { outcome: "EscalatedAbandoned", tone: "bad" },
  { outcome: "FailedAgentLimitation", tone: "bad" },
];

function isoDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function daysAgo(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return isoDate(date);
}

/** How the ordering line is doing.
 *
 * ⚠ Built on calls, not on orders, and the difference is what the page is for. An orders-only
 * view cannot see the calls that produced nothing — and a line taking forty orders from a hundred
 * calls and one taking forty from forty-two look identical until you count both.
 *
 * Every tile opens the calls it counted. A number you cannot open is a number you have to take on
 * trust, and this page exists to be checked. */
export function OrderDashboardPage() {
  const { token } = useAuth();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();
  const navigate = useNavigate();

  const [from, setFrom] = useState(() => daysAgo(30));
  const [to, setTo] = useState(() => isoDate(new Date()));

  const state = useApiData(
    () =>
      getOrderDashboard(
        token!,
        new Date(`${from}T00:00:00`).toISOString(),
        new Date(`${to}T23:59:59.999`).toISOString(),
      ),
    [token, from, to],
    { keepPrevious: true },
  );

  function openCalls(outcome?: CallOutcome) {
    const query = new URLSearchParams({ from, to });
    if (outcome) query.set("outcome", outcome);
    navigate(`/orders/calls?${query.toString()}`);
  }

  const data = state.status === "success" ? state.data : null;

  // Ranked by how many orders asked for it, and drawn on that. Quantities are in each product's
  // own unit, so three bidons and three kilos are not three of the same thing — a chart of them
  // would be comparing lengths that mean nothing to each other.
  const productShares: Share[] = (data?.byProduct ?? []).map((p) => ({ label: p.productName, count: p.orders }));

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
      {data && (
        <>
          <div className="outcome-grid">
            <OutcomeTile label={t("totalCalls")} count={data.totalCalls} tone="total" onClick={() => openCalls()} />
            {OUTCOMES.map(({ outcome, tone }) => {
              const row = data.byOutcome.find((o) => o.outcome === outcome);
              return (
                <OutcomeTile
                  key={outcome}
                  label={translateEnum(callOutcomeLabels, outcome, language)}
                  count={row?.calls ?? 0}
                  share={row?.share ?? 0}
                  tone={tone}
                  onClick={() => openCalls(outcome)}
                />
              );
            })}
          </div>

          {/* ⚠ Separate from the outcome tiles, and deliberately not one of them. A call can
              resolve perfectly and take no order — the caller asked a question and got an answer —
              so "did it work" and "did it sell" are two readings of the same call, not one. */}
          <Card style={{ marginTop: "var(--space-4)" }}>
            <h2>{t("orderResults")}</h2>
            <div className="stat-row">
              <StatTile
                label={t("callsWithOrder")}
                value={String(data.callsWithOrder)}
                sub={data.orderRate === null ? undefined : `${Math.round(data.orderRate * 100)}%`}
              />
              <StatTile label={t("ordersPlaced")} value={String(data.ordersPlaced)} />
              <StatTile label={t("ordersCancelled")} value={String(data.ordersCancelled)} />
              <StatTile label={t("totalOrderValue")} value={`${data.totalValueAzn.toFixed(2)} ₼`} />
              {/* Dashes rather than 0.00: an average over no orders is not zero, and zero would
                  read as a line taking orders worth nothing. */}
              <StatTile
                label={t("averageOrderValue")}
                value={data.averageOrderValueAzn === null ? "—" : `${data.averageOrderValueAzn.toFixed(2)} ₼`}
              />
            </div>
          </Card>

          <Card>
            <h2>{t("callStatistics")}</h2>
            <div className="stat-row">
              <StatTile label={t("totalCallMinutes")} value={formatDuration(data.totalDurationSeconds)} />
              <StatTile
                label={t("avgCallDuration")}
                value={formatDuration(Math.round(data.averageCallDurationSeconds))}
              />
              <StatTile
                label={t("avgOrderingCallDuration")}
                value={
                  data.averageOrderingCallDurationSeconds === null
                    ? "—"
                    : formatDuration(Math.round(data.averageOrderingCallDurationSeconds))
                }
              />
            </div>
          </Card>

          {/* Same form rule as the questionnaire results: up to three shares get a pie, more get
              bars, because a fourth hue lands inside the colour-vision floor. See SharePie. */}
          <Card>
            <h2>{t("whatWasOrdered")}</h2>
            {productShares.length === 0 ? (
              <div className="note">{t("noOrdersYet")}</div>
            ) : (
              <>
                {productShares.length <= MAX_PIE_SLICES ? (
                  <SharePie shares={productShares} />
                ) : (
                  <ShareBars shares={productShares} />
                )}
                <div className="table-scroll" style={{ marginTop: "var(--space-4)" }}>
                  <table className="data striped">
                    <thead>
                      <tr>
                        <th>{t("product")}</th>
                        <th className="mono">{t("colOrders")}</th>
                        <th className="mono">{t("colQuantity")}</th>
                        <th className="mono">{t("totalOrderValue")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.byProduct.map((p) => (
                        <tr key={p.productId}>
                          <td>{p.productName}</td>
                          <td className="mono">{p.orders}</td>
                          <td className="mono">{p.quantity}</td>
                          <td className="mono">{p.valueAzn.toFixed(2)} ₼</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {/* Said rather than left for somebody to work out from a chart that looks
                    comparable and is not. */}
                <div className="note">{t("quantitiesArePerUnit")}</div>
              </>
            )}
          </Card>
        </>
      )}
    </AppShell>
  );
}
