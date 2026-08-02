import { useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Button } from "@/shared/components/Button";
import { TimeGridCalendar } from "@/modules/appointments/components/TimeGridCalendar";
import { MonthCalendarGrid } from "@/modules/appointments/components/MonthCalendarGrid";
import { AppointmentPanel } from "@/modules/appointments/components/AppointmentPanel";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { getAppointments } from "@/modules/appointments/api/appointments";
import { getProviders } from "@/modules/appointments/api/providers";
import { getServiceOfferings } from "@/modules/appointments/api/serviceOfferings";
import type { AppointmentResponse } from "@/shared/api/types";
import { makeProviderColorLookup } from "@/modules/appointments/lib/providerColors";
import { useLanguage } from "@/shared/i18n/LanguageContext";

const HOURS = [9, 10, 11, 12, 13, 14, 15, 16, 17, 18];

type CalendarView = "day" | "week" | "month";

function startOfWeek(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  const diff = (day === 0 ? -6 : 1) - day; // Monday as first day
  d.setDate(d.getDate() + diff);
  d.setHours(0, 0, 0, 0);
  return d;
}

function startOfDay(date: Date): Date {
  const d = new Date(date);
  d.setHours(0, 0, 0, 0);
  return d;
}

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

/** Range shown to the visitor covers the full 6-week grid a month view renders, even though
 * the fetched appointments only need to span the visible weeks either side of the month. */
function monthGridRange(monthStart: Date): { from: Date; to: Date } {
  const firstWeekday = (monthStart.getDay() + 6) % 7;
  const from = new Date(monthStart);
  from.setDate(from.getDate() - firstWeekday);
  const to = new Date(from);
  to.setDate(to.getDate() + 42);
  return { from, to };
}

export function CalendarPage() {
  const { token } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const [view, setView] = useState<CalendarView>("week");
  const [anchorDate, setAnchorDate] = useState(() => new Date());
  const [providerFilter, setProviderFilter] = useState<string>("");
  const [panelState, setPanelState] = useState<{ mode: "create" | "edit"; appt?: AppointmentResponse; start?: string } | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const dayStart = startOfDay(anchorDate);
  const weekStart = startOfWeek(anchorDate);
  const monthStart = startOfMonth(anchorDate);

  const rangeStart = view === "day" ? dayStart : view === "week" ? weekStart : monthGridRange(monthStart).from;
  const rangeEnd =
    view === "day"
      ? new Date(dayStart.getTime() + 86_400_000)
      : view === "week"
        ? new Date(weekStart.getTime() + 7 * 86_400_000)
        : monthGridRange(monthStart).to;

  const providersState = useApiData(() => getProviders(token!), [token]);
  const servicesState = useApiData(() => getServiceOfferings(token!), [token]);
  const appointmentsState = useApiData(
    () => getAppointments(token!, rangeStart.toISOString(), rangeEnd.toISOString(), providerFilter ? Number(providerFilter) : undefined),
    [token, view, rangeStart.getTime(), rangeEnd.getTime(), providerFilter, refreshKey],
  );

  const providers = providersState.status === "success" ? providersState.data : [];
  const services = servicesState.status === "success" ? servicesState.data : [];
  const appointments = appointmentsState.status === "success" ? appointmentsState.data : [];
  const providerColor = makeProviderColorLookup(providers);

  function serviceName(id: number) {
    return services.find((s) => s.id === id)?.name ?? t("service");
  }

  function shiftRange(direction: 1 | -1) {
    const next = new Date(anchorDate);
    if (view === "day") next.setDate(next.getDate() + direction);
    else if (view === "week") next.setDate(next.getDate() + direction * 7);
    else next.setMonth(next.getMonth() + direction);
    setAnchorDate(next);
  }

  function goToToday() {
    setAnchorDate(new Date());
  }

  function switchView(nextView: CalendarView) {
    setView(nextView);
  }

  function rangeLabel(): string {
    if (view === "day") return dayStart.toLocaleDateString();
    if (view === "week") {
      const weekEnd = new Date(weekStart.getTime() + 6 * 86_400_000);
      return `${weekStart.toLocaleDateString()} – ${weekEnd.toLocaleDateString()}`;
    }
    return monthStart.toLocaleDateString(undefined, { month: "long", year: "numeric" });
  }

  return (
    <AppShell {...shell}>
      <div className="toolbar">
        <div className="date-nav">
          <span className="step" onClick={() => shiftRange(-1)}>
            ‹
          </span>{" "}
          {rangeLabel()}{" "}
          <span className="step" onClick={() => shiftRange(1)}>
            ›
          </span>{" "}
          <span className="step" onClick={goToToday}>
            {t("today")}
          </span>
        </div>
        <div className="view-toggle">
          <span className={view === "day" ? "active" : ""} onClick={() => switchView("day")}>
            {t("viewDay")}
          </span>
          <span className={view === "week" ? "active" : ""} onClick={() => switchView("week")}>
            {t("viewWeek")}
          </span>
          <span className={view === "month" ? "active" : ""} onClick={() => switchView("month")}>
            {t("viewMonth")}
          </span>
        </div>
        <select value={providerFilter} onChange={(e) => setProviderFilter(e.target.value)}>
          <option value="">{t("allProviders")}</option>
          {providers.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
        <Button onClick={() => setPanelState({ mode: "create" })}>{t("newAppointment")}</Button>
      </div>

      {appointmentsState.status === "error" && <div className="field-error">{t("failedToLoadAppointments")}</div>}

      {providers.length > 1 && (
        <div className="provider-legend">
          {providers.map((p) => (
            <span className="legend-chip" key={p.id}>
              <span className="legend-dot" style={{ background: providerColor(p.id).border }} />
              {p.name}
            </span>
          ))}
        </div>
      )}

      {view === "month" ? (
        <MonthCalendarGrid
          monthStart={monthStart}
          appointments={appointments}
          serviceName={serviceName}
          providerColor={providerColor}
          onApptClick={(appt) => setPanelState({ mode: "edit", appt })}
          onDayClick={(day) => {
            setAnchorDate(day);
            setView("day");
          }}
        />
      ) : (
        <TimeGridCalendar
          days={view === "day" ? [dayStart] : Array.from({ length: 7 }, (_, i) => new Date(weekStart.getTime() + i * 86_400_000))}
          hours={HOURS}
          appointments={appointments}
          serviceName={serviceName}
          providerColor={providerColor}
          onApptClick={(appt) => setPanelState({ mode: "edit", appt })}
        />
      )}

      {panelState && (
        <AppointmentPanel
          providers={providers}
          serviceOfferings={services}
          existing={panelState.mode === "edit" ? panelState.appt : undefined}
          defaultStart={panelState.start}
          onClose={() => setPanelState(null)}
          onSaved={() => {
            setPanelState(null);
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
    </AppShell>
  );
}
