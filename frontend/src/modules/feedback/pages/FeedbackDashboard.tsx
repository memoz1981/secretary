import { useEffect, useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { StatTile } from "@/shared/components/StatTile";
import { MAX_PIE_SLICES, SharePie, ShareBars, type Share } from "@/shared/components/SharePie";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { getFeedbackDashboard, getSurveys } from "@/modules/feedback/api/feedback";
import type { QuestionResult } from "@/shared/api/types";

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

function percent(value: number | null): string {
  return value === null ? "—" : `${Math.round(value * 100)}%`;
}

/** What the customers said. Nothing here is about the machine.
 *
 * ⚠ Cost, duration, tokens and attempts used to sit at the top of this page under the heading
 * "agent quality", and they answer a different person's question. They live on the Calls page
 * now; the only process number that survives here is coverage, because a score means nothing
 * without knowing how many people it came from. */
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
  const trend =
    data?.scorePercent != null && data.previousScorePercent != null
      ? data.scorePercent - data.previousScorePercent
      : null;

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
          {/* The one number, and never without what it rests on. 92% from four people is not
              92%, so the count and the response rate sit under it rather than elsewhere. */}
          <Card>
            <div className="score-figure mono">
              {data.scorePercent === null ? "—" : `${Math.round(data.scorePercent)}%`}
            </div>
            <div className="share-label">{t("satisfactionScore")}</div>
            {trend !== null && (
              <div className={`score-trend ${trend >= 0 ? "up" : "down"}`}>
                {trend >= 0 ? "▲" : "▼"} {Math.abs(Math.round(trend))} {t("vsPreviousPeriod")}
              </div>
            )}
            <div className="score-caption">
              {data.scorePercent === null
                ? t("noScoredQuestions")
                : t("basedOnAnswers").replace("{n}", String(data.scoreAnswerCount))}
            </div>
          </Card>

          {/* Coverage, which qualifies everything above it. Counted in people — the version this
              replaces counted rows in the calls table, so four dead connections in nine seconds
              read as four surveys attempted. */}
          <Card>
            <h2>{t("coverage")}</h2>
            <div className="stat-row">
              <StatTile label={t("peopleRequested")} value={String(data.coverage.requested)} />
              <StatTile label={t("peopleReached")} value={String(data.coverage.reached)} />
              <StatTile label={t("surveysCompleted")} value={String(data.coverage.completed)} />
              <StatTile label={t("responseRate")} value={percent(data.coverage.responseRate)} />
              <StatTile label={t("completionRate")} value={percent(data.coverage.completionRate)} />
              <StatTile label={t("notReached")} value={String(data.coverage.notReached)} />
              <StatTile label={t("needsAPerson")} value={String(data.coverage.needsHuman)} />
            </div>
            <div className="note">{t("coverageExplain")}</div>
          </Card>

          <Card>
            <h2>{t("results")}</h2>
            {data.results.length === 0 ? (
              <div className="note">{t("noChoiceQuestions")}</div>
            ) : (
              data.results.map((result) => <QuestionPanel key={result.questionId} result={result} />)
            )}
            {/* Said plainly rather than left as an absence somebody has to notice. */}
            <div className="note">{t("openQuestionsNotCharted")}</div>
          </Card>
        </>
      )}
    </AppShell>
  );
}

/** One question.
 *
 * ⚠ The form follows the slice count, not the question type. Up to three shares get a pie;
 * beyond that they get bars, because a pie needs every slice separable from every other and a
 * fourth hue lands inside the colour-vision floor — a six-slice set had a worst pair at ΔE 0.5,
 * which is to say two slices nobody could tell apart. See SharePie.tsx. */
function QuestionPanel({ result }: { result: QuestionResult }) {
  const { t } = useLanguage();
  const shares: Share[] = result.options.map((o) => ({ label: o.text, count: o.count }));
  const total = shares.reduce((sum, s) => sum + s.count, 0);

  return (
    <div className="question-panel">
      <h3>
        {result.position + 1}. {result.questionText}
      </h3>
      <div className="meta mono">
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

      {total === 0 ? (
        <div className="note">{t("noDataYet")}</div>
      ) : shares.length <= MAX_PIE_SLICES ? (
        <SharePie shares={shares} />
      ) : (
        <ShareBars shares={shares} />
      )}
    </div>
  );
}
