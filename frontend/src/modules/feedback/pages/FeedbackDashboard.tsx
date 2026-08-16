import { useEffect, useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { StatTile } from "@/shared/components/StatTile";
import { BarRow } from "@/shared/components/BarRow";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDuration, formatUsd } from "@/shared/lib/money";
import { getFeedbackDashboard, getSurveys } from "@/modules/feedback/api/feedback";

type Range = "week" | "month" | "all";

/** Trailing windows, and the labels say so.
 *
 * "Last 7 days" rather than "this week" because that is what the arithmetic does — the
 * appointment dashboard calls the same computation "this month" and is wrong about it on the
 * 31st. Anchored at midnight so the numbers do not drift while somebody watches the page. */
function since(range: Range): string | undefined {
  if (range === "all") return undefined;

  const from = new Date();
  from.setHours(0, 0, 0, 0);
  from.setDate(from.getDate() - (range === "week" ? 7 : 30));
  return from.toISOString();
}

export function FeedbackDashboardPage() {
  const { token } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();

  const [surveyId, setSurveyId] = useState<number | null>(null);
  const [range, setRange] = useState<Range>("month");

  const surveys = useApiData(() => getSurveys(token!), [token]);

  // A questionnaire has to be chosen before anything can be shown: averaging two different sets
  // of questions together would be a number about nothing. Defaulting to the first spares the
  // Owner a click for the common case of having one.
  useEffect(() => {
    if (surveyId === null && surveys.status === "success" && surveys.data.length > 0) {
      setSurveyId(surveys.data[0].id);
    }
  }, [surveys, surveyId]);

  const state = useApiData(
    () =>
      surveyId === null
        ? Promise.resolve(null)
        : getFeedbackDashboard(token!, surveyId, { from: since(range) }),
    [token, surveyId, range],
  );

  const data = state.status === "success" ? state.data : null;

  // Every question the owner ticked, averaged into one percentage. Replaced a single "headline"
  // question, because "Məmnun qaldınız?" is satisfaction and "Servis kitabçası verildi?" is a
  // fact — one number built from both moves for two unrelated reasons.
  const counting = (data?.results ?? []).filter((r) => r.countsTowardScore && r.averagePercent !== null);
  const score =
    counting.length === 0
      ? null
      : counting.reduce((sum, r) => sum + (r.averagePercent ?? 0), 0) / counting.length;
  const scoreAnswers = counting.reduce((sum, r) => sum + r.answeredCount, 0);

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("feedbackDashboard")}
      </h1>

      <div className="filters" style={{ display: "flex", gap: "var(--space-3)", marginBottom: "var(--space-4)" }}>
        <select value={surveyId ?? ""} onChange={(e) => setSurveyId(e.target.value === "" ? null : Number(e.target.value))}>
          {surveys.status === "success" &&
            surveys.data.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
        </select>
        <select value={range} onChange={(e) => setRange(e.target.value as Range)}>
          <option value="week">{t("lastSevenDays")}</option>
          <option value="month">{t("lastThirtyDays")}</option>
          <option value="all">{t("allTime")}</option>
        </select>
      </div>

      {surveys.status === "success" && surveys.data.length === 0 && (
        <div className="note">{t("needAQuestionnaireFirst")}</div>
      )}

      {data && (
        <>
          {/* The one number, and never without its denominator: 92% from four people is not 92%. */}
          {score !== null && (
            <Card>
              <div className="headline-metric">
                <div className="headline-value mono">{Math.round(score)}%</div>
                <div className="headline-label">{t("satisfactionScore")}</div>
                <div className="headline-sub">
                  {t("basedOnAnswers").replace("{n}", String(scoreAnswers))}
                </div>
              </div>
            </Card>
          )}

          {/* How the agent itself did. Same shape whatever was asked, which is the point —
              it says whether the thing works, not what customers think. */}
          <Card>
            <h2>{t("agentQuality")}</h2>
            <div className="stat-row">
              <StatTile label={t("callsQueued")} value={String(data.agent.callsQueued)} />
              <StatTile label={t("callsStarted")} value={String(data.agent.callsStarted)} />
              <StatTile label={t("callsCompleted")} value={String(data.agent.callsCompleted)} />
              <StatTile label={t("callsAbandoned")} value={String(data.agent.callsAbandoned)} />
              <StatTile
                label={t("completionRate")}
                value={
                  data.agent.completionRate === null
                    ? "—"
                    : `${Math.round(data.agent.completionRate * 100)}%`
                }
              />
              <StatTile label={t("avgDuration")} value={formatDuration(data.agent.averageDurationSeconds)} />
              <StatTile label={t("totalCost")} value={formatUsd(data.agent.totalCostUsd)} />
            </div>
          </Card>

          {/* Where people hang up. Needs no knowledge of the questions, and is usually the most
              actionable thing here — a question that loses a third of callers is one to rewrite. */}
          <Card>
            <h2>{t("whereCallersStop")}</h2>
            {data.dropOff.length === 0 ? (
              <div className="note">{t("noDataYet")}</div>
            ) : (
              data.dropOff.map((d) => (
                <BarRow
                  key={d.questionId}
                  label={`${d.position + 1}. ${d.questionText} — ${d.answered}/${d.reached}`}
                  pct={d.reached === 0 ? 0 : (d.answered / d.reached) * 100}
                />
              ))
            )}
          </Card>

          <Card>
            <h2>{t("results")}</h2>
            {data.results.length === 0 ? (
              <div className="note">{t("noChoiceQuestions")}</div>
            ) : (
              data.results.map((result) => {
                const total = result.options.reduce((sum, o) => sum + o.count, 0);
                return (
                  <div key={result.questionId} className="result-block">
                    <div className="result-question">
                      {result.position + 1}. {result.questionText}
                    </div>
                    <div className="result-meta mono">
                      {result.isScored && result.averagePercent !== null && (
                        <span>
                          {t("average")} {Math.round(result.averagePercent)}% ·{" "}
                        </span>
                      )}
                      {t("answeredN").replace("{n}", String(result.answeredCount))}
                      {result.declinedCount > 0 && (
                        <span> · {t("declinedN").replace("{n}", String(result.declinedCount))}</span>
                      )}
                    </div>
                    {result.options.map((o) => (
                      <BarRow
                        key={o.optionId}
                        label={`${o.text}${o.scorePercent === null ? "" : ` (${o.scorePercent}%)`} — ${o.count}`}
                        pct={total === 0 ? 0 : (o.count / total) * 100}
                      />
                    ))}
                  </div>
                );
              })
            )}
            {/* Said plainly rather than left as an absence somebody has to notice. */}
            <div className="note">{t("openQuestionsNotCharted")}</div>
          </Card>
        </>
      )}
    </AppShell>
  );
}
