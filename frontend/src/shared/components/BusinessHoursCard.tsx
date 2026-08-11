import { useState } from "react";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { getBusinessHours, updateBusinessHours } from "@/shared/api/businessHours";
import type { BusinessHoursDay } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { dayOfWeekLabels, translateEnum } from "@/shared/i18n/translations";

/** Opening hours, on Administration because a business has one set of them however many modules
 *  it holds. Appointments will not offer a slot outside them; Orders will not promise a delivery
 *  outside them.
 *
 *  Saved as a whole week. A per-day save would let someone close Tuesday and never notice
 *  Wednesday had never been set — and a day with no hours is closed, so not noticing means
 *  silently turning custom away. */
export function BusinessHoursCard({ canEdit }: { canEdit: boolean }) {
  const { token } = useAuth();
  const { language, t } = useLanguage();
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useApiData(() => getBusinessHours(token!), [token, refreshKey]);
  const [draft, setDraft] = useState<BusinessHoursDay[] | null>(null);
  const [saving, setSaving] = useState(false);

  const days = draft ?? (state.status === "success" ? state.data : []);

  function set(index: number, next: Partial<BusinessHoursDay>) {
    setDraft(days.map((d, i) => (i === index ? { ...d, ...next } : d)));
  }

  return (
    <Card>
      <h2 style={{ marginBottom: "var(--space-3)" }}>{t("businessHours")}</h2>
      <p className="sub">{t("businessHoursHelp")}</p>
      {state.status === "error" && <div className="field-error">{t("failedToLoadBusinessHours")}</div>}

      <div className="hours-grid">
        {days.map((day, index) => {
          const closed = day.opensAt === null || day.closesAt === null;
          return (
            <div className="hours-row" key={day.dayOfWeek}>
              <span className="hours-day">{translateEnum(dayOfWeekLabels, day.dayOfWeek, language)}</span>
              <label className="hours-closed">
                <input
                  type="checkbox"
                  checked={closed}
                  disabled={!canEdit}
                  onChange={(e) =>
                    set(index, e.target.checked ? { opensAt: null, closesAt: null } : { opensAt: "09:00", closesAt: "18:00" })
                  }
                />
                {t("closed")}
              </label>
              <input
                type="time"
                className="input"
                value={day.opensAt ?? ""}
                disabled={!canEdit || closed}
                onChange={(e) => set(index, { opensAt: e.target.value })}
              />
              <span className="hours-dash">–</span>
              <input
                type="time"
                className="input"
                value={day.closesAt ?? ""}
                disabled={!canEdit || closed}
                onChange={(e) => set(index, { closesAt: e.target.value })}
              />
            </div>
          );
        })}
      </div>

      {canEdit && (
        <div className="actions">
          <Button
            loading={saving}
            disabled={draft === null}
            onClick={async () => {
              setSaving(true);
              try {
                await updateBusinessHours(token!, days);
                setDraft(null);
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
