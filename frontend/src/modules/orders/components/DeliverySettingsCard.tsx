import { useState } from "react";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { TextField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { getOrderSettings, updateOrderSettings } from "@/modules/orders/api/orders";
import { useLanguage } from "@/shared/i18n/LanguageContext";

/** Delivery settings, on Administration rather than the Orders page.
 *
 * They sit next to opening hours because that is what they are made of: the promise counts
 * working days, and the window is how far ahead this business will commit. Both are things a
 * tenant sets once, not something to trip over above a list of today's orders.
 *
 * The one place a module's own setting appears on the shared page — so the page only renders it
 * for a tenant who holds the module. */
export function DeliverySettingsCard({ canEdit }: { canEdit: boolean }) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useApiData(() => getOrderSettings(token!), [token, refreshKey]);
  const [lead, setLead] = useState<string | null>(null);
  const [window, setWindow] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (state.status !== "success") {
    return null;
  }

  const leadValue = lead ?? String(state.data.leadWorkingDays);
  const windowValue = window ?? String(state.data.maxDeliveryDaysAhead);

  return (
    <Card>
      <h2 style={{ marginBottom: "var(--space-3)" }}>{t("deliverySettings")}</h2>

      <TextField
        label={t("leadWorkingDays")}
        value={leadValue}
        disabled={!canEdit}
        onChange={(e) => setLead(e.target.value)}
      />
      <div className="sub" style={{ marginTop: "calc(var(--space-2) * -1)" }}>{t("deliveryPromiseHelp")}</div>

      <TextField
        label={t("maxDeliveryDaysAhead")}
        value={windowValue}
        disabled={!canEdit}
        onChange={(e) => setWindow(e.target.value)}
      />
      <div className="sub" style={{ marginTop: "calc(var(--space-2) * -1)" }}>{t("maxDeliveryDaysAheadHelp")}</div>

      {error && <div className="field-error">{error}</div>}

      {canEdit && (
        <div className="actions">
          <Button
            loading={saving}
            disabled={lead === null && window === null}
            onClick={async () => {
              const leadDays = Number(leadValue);
              const windowDays = Number(windowValue);

              // Checked here as well as on the server, because the message is the point: a
              // window shorter than the promise refuses every day including the one the agent
              // offers first, and the failure would look like the agent being broken.
              if (Number.isNaN(leadDays) || leadDays < 0 || leadDays > 14) {
                setError(t("leadWorkingDaysRange"));
                return;
              }

              if (Number.isNaN(windowDays) || windowDays < 1 || windowDays > 365 || windowDays < leadDays) {
                setError(t("maxDeliveryDaysAheadRange"));
                return;
              }

              setError(null);
              setSaving(true);
              try {
                await updateOrderSettings(token!, leadDays, windowDays);
                setLead(null);
                setWindow(null);
                setRefreshKey((k) => k + 1);
              } finally {
                setSaving(false);
              }
            }}
          >
            {t("save")}
          </Button>
        </div>
      )}
    </Card>
  );
}
