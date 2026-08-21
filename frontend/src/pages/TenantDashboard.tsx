import { useNavigate, useParams } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Button } from "@/shared/components/Button";
import { Card } from "@/shared/components/Card";
import { StatTile } from "@/shared/components/StatTile";
import { ShareBars, type Share } from "@/shared/components/SharePie";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDuration, formatTokens, formatUsd } from "@/shared/lib/money";
import { getTenantInsights } from "@/shared/api/tenants";
import { CALL_CATEGORIES } from "@/shared/api/types";
import type { Module } from "@/shared/api/types";
import type { TranslationKey } from "@/shared/i18n/translations";

/** The module titles this page can show. Mapped rather than built from the name, so a module
 *  whose key and translation key disagree is a compile error rather than a blank cell. */
const MODULE_LABEL: Record<Module, TranslationKey> = {
  Appointment: "moduleAppointmentsTitle",
  Order: "moduleOrdersTitle",
  Feedback: "moduleFeedbackTitle",
  Information: "moduleInfoTitle",
  Reminder: "moduleRemindersTitle",
  Survey: "moduleSurveysTitle",
};

const CATEGORY_LABEL: Record<(typeof CALL_CATEGORIES)[number], TranslationKey> = {
  Answered: "categoryAnswered",
  Forwarded: "categoryForwarded",
  Unfinished: "categoryUnfinished",
  NotAnswered: "categoryNotAnswered",
};

/** One tenant, from the platform's side: what they have used and what it has cost us.
 *
 * ⚠ Not the tenant's own dashboard with a different header. Theirs answers "how is my appointment
 * line doing", in that module's words, scoped to them. This answers "what is this account doing
 * and what is it costing", across every module at once — which needed a vocabulary the three
 * modules share, because appointments and orders record booking outcomes and feedback records how
 * far through a questionnaire somebody got. See CallCategories.
 *
 * Money is unconditional here. Everywhere else it goes through CallCostVisibility; this page only
 * exists for the caller that check always says yes to. */
export function TenantDashboardPage() {
  const { token } = useAuth();
  const { id } = useParams<{ id: string }>();
  const tenantId = Number(id);
  const navigate = useNavigate();
  const { t } = useLanguage();
  const shell = usePlatformAdminShell();

  const state = useApiData(() => getTenantInsights(token!, tenantId), [token, id]);

  if (state.status === "loading") {
    return (
      <AppShell {...shell}>
        <div className="note">{t("loading")}</div>
      </AppShell>
    );
  }

  if (state.status === "error") {
    return (
      <AppShell {...shell}>
        <div className="field-error">{t("couldNotLoad")}</div>
      </AppShell>
    );
  }

  const data = state.data;
  const modules = data.byModule.filter((m) => m.calls > 0);

  // Every category, including the empty ones — "nothing was forwarded" is one of the more useful
  // things this page can say, and a missing row reads as a category that does not exist.
  const categories: Share[] = data.byCategory.map((c) => ({
    label: t(CATEGORY_LABEL[c.category]),
    count: c.calls,
  }));

  return (
    <AppShell {...shell}>
      <Button size="sm" variant="secondary" onClick={() => navigate(`/admin/tenants/${tenantId}`)}>
        {t("backToTenant")}
      </Button>

      <h1 className="page-title" style={{ marginTop: "var(--space-3)" }}>
        {data.tenantName}
      </h1>
      <div className="subtitle">{t("tenantDashboardSubtitle")}</div>

      <Card>
        <h2>{t("usage")}</h2>
        <div className="stat-row">
          <StatTile label={t("totalCalls")} value={String(data.totalCalls)} />
          <StatTile label={t("totalSpend")} value={formatUsd(data.totalCostUsd)} />
          <StatTile label={t("avgCostPerCall")} value={formatUsd(data.averageCostPerCallUsd)} />
          <StatTile label={t("totalMinutes")} value={formatDuration(data.totalDurationSeconds)} />
          <StatTile
            label={t("avgDuration")}
            value={data.averageDurationSeconds === null ? "—" : formatDuration(data.averageDurationSeconds)}
          />
          <StatTile label={t("totalTokens")} value={formatTokens(data.totalTokens)} />
        </div>
      </Card>

      <Card>
        <h2>{t("howCallsEnded")}</h2>
        {data.totalCalls === 0 ? (
          <div className="note">{t("noCallsYet")}</div>
        ) : (
          <ShareBars shares={categories} />
        )}
      </Card>

      <Card>
        <h2>{t("byModule")}</h2>
        {modules.length === 0 ? (
          <div className="note">{t("noCallsYet")}</div>
        ) : (
          <div className="table-scroll">
            <table className="data striped">
              <thead>
                <tr>
                  <th>{t("module")}</th>
                  <th className="mono">{t("totalCalls")}</th>
                  <th className="mono">{t("totalMinutes")}</th>
                  <th className="mono">{t("totalTokens")}</th>
                  <th className="mono">{t("totalSpend")}</th>
                </tr>
              </thead>
              <tbody>
                {modules.map((m) => (
                  <tr key={m.module}>
                    <td>{t(MODULE_LABEL[m.module])}</td>
                    <td className="mono">{m.calls}</td>
                    <td className="mono">{formatDuration(m.durationSeconds)}</td>
                    <td className="mono">{formatTokens(m.tokens)}</td>
                    <td className="mono">{formatUsd(m.costUsd)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </AppShell>
  );
}
