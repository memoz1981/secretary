import { useState } from "react";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { getBusinessHours, updateBusinessHours } from "@/shared/api/businessHours";
import type { BusinessHoursDay } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { dayOfWeekLabels, translateEnum } from "@/shared/i18n/translations";
import { TimeSelect } from "@/shared/components/TimeSelect";

const DEFAULT_OPEN = "09:00";
const DEFAULT_CLOSE = "21:00";

/** Opening hours, on Administration because a business has one set of them however many modules
 *  it holds. Appointments will not offer a slot outside them; Orders will not promise a delivery
 *  outside them.
 *
 *  ⚠ One pair of hours for the whole week, and a tick per day for the days the business is shut.
 *  It used to be a full open/close pair on each of seven rows, which is fourteen time fields to
 *  set the same two numbers, and it invited a week where Tuesday quietly closed an hour earlier
 *  than the sign on the door.
 *
 *  The storage underneath is still per day — CallingHours, the availability tools and the
 *  delivery promise all read it that way, and nothing about this screen is a reason to change
 *  what they read. Saving writes the one pair onto every open day. A week that already held
 *  different hours per day therefore flattens on the next save, which is the point rather than a
 *  side effect; the hours shown are the ones the first open day had.
 *
 *  Saved as a whole week. A per-day save would let someone close Tuesday and never notice
 *  Wednesday had never been set — and a day with no hours is closed, so not noticing means
 *  silently turning custom away. */
export function BusinessHoursCard({ canEdit }: { canEdit: boolean }) {
  const { token } = useAuth();
  const { language, t } = useLanguage();
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useApiData(() => getBusinessHours(token!), [token, refreshKey], { keepPrevious: true });
  const [draft, setDraft] = useState<BusinessHoursDay[] | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const days = draft ?? (state.status === "success" ? state.data : []);
  const isClosed = (day: BusinessHoursDay) => day.opensAt === null || day.closesAt === null;

  // The hours the week is actually keeping. Taken from the first open day, because that is the
  // one whose numbers a reader would check the card against.
  const open = days.find((d) => !isClosed(d));
  const opensAt = open?.opensAt ?? DEFAULT_OPEN;
  const closesAt = open?.closesAt ?? DEFAULT_CLOSE;

  /** Every open day carries the same pair; closed days keep their nulls. */
  function setHours(next: { opensAt?: string; closesAt?: string }) {
    setSaved(false);
    setDraft(
      days.map((day) =>
        isClosed(day)
          ? day
          : { ...day, opensAt: next.opensAt ?? opensAt, closesAt: next.closesAt ?? closesAt },
      ),
    );
  }

  function setClosed(dayOfWeek: string, closed: boolean) {
    setSaved(false);
    setDraft(
      days.map((day) =>
        day.dayOfWeek === dayOfWeek
          ? closed
            ? { ...day, opensAt: null, closesAt: null }
            : { ...day, opensAt, closesAt }
          : day,
      ),
    );
  }

  return (
    <Card>
      <h2 style={{ marginBottom: "var(--space-3)" }}>{t("businessHours")}</h2>
      <p className="sub">{t("businessHoursHelp")}</p>
      {state.status === "error" && <div className="field-error">{t("failedToLoadBusinessHours")}</div>}

      <div className="filter-bar" style={{ marginBottom: "var(--space-4)" }}>
        <label className="filter-field">
          <span className="label">{t("openingTime")}</span>
          <TimeSelect value={opensAt} disabled={!canEdit} onChange={(next) => setHours({ opensAt: next })} />
        </label>
        <label className="filter-field">
          <span className="label">{t("closingTime")}</span>
          <TimeSelect value={closesAt} disabled={!canEdit} onChange={(next) => setHours({ closesAt: next })} />
        </label>
      </div>
      <p className="sub">{t("businessHoursOneSet")}</p>

      <div className="table-scroll">
        <table className="data striped">
          <thead>
            <tr>
              <th>{t("colDay")}</th>
              <th>{t("closed")}</th>
              <th className="mono">{t("businessHours")}</th>
            </tr>
          </thead>
          <tbody>
            {days.map((day) => {
              const closed = isClosed(day);
              return (
                <tr key={day.dayOfWeek} className={closed ? "unused" : ""}>
                  <td>{translateEnum(dayOfWeekLabels, day.dayOfWeek, language)}</td>
                  <td>
                    <input
                      type="checkbox"
                      checked={closed}
                      disabled={!canEdit}
                      onChange={(e) => setClosed(day.dayOfWeek, e.target.checked)}
                    />
                  </td>
                  {/* Repeated per row rather than left blank: the point of the table is which
                      days are open, and a row that says nothing about its hours makes the reader
                      go back up to the fields to work out what it means. */}
                  <td className="mono">{closed ? "—" : `${opensAt} – ${closesAt}`}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {canEdit && (
        <>
          <div className="actions">
            <Button
              loading={saving}
              disabled={draft === null}
              onClick={async () => {
                setSaving(true);
                try {
                  await updateBusinessHours(token!, days);
                  setDraft(null);
                  setSaved(true);
                  setRefreshKey((k) => k + 1);
                } finally {
                  setSaving(false);
                }
              }}
            >
              {t("save")}
            </Button>
          </div>
          {saved && draft === null && <div className="save-note">{t("savedNote")}</div>}
        </>
      )}
    </Card>
  );
}
