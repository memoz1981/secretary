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
import { getBusinessHours } from "@/shared/api/businessHours";
import { getProviders } from "@/modules/appointments/api/providers";
import { getServiceOfferings } from "@/modules/appointments/api/serviceOfferings";
import type { AppointmentResponse } from "@/shared/api/types";
import { makeProviderColorLookup } from "@/modules/appointments/lib/providerColors";
import { useLanguage } from "@/shared/i18n/LanguageContext";

/** The grid when opening hours have not loaded yet, or when every day is marked closed.
 *
 * ⚠ This used to be the grid, full stop — hard-coded 9 to 18 while Administration let a tenant
 * set anything they liked. A business opening at 08:00 had an hour of its day missing from the
 * calendar, and one closing at 20:00 had two. */
const FALLBACK_HOURS = [9, 10, 11, 12, 13, 14, 15, 16, 17, 18];

/** JavaScript counts weeks from Sunday; the API names its days. */
const DAY_NAMES = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

/** "09:00" to 9. A half-hour opening rounds down so the first slot is still on the grid, and a
 *  half-hour closing rounds up so the last one is. */
function hourOf(time: string, round: "down" | "up"): number {
  const [hours, minutes] = time.split(":").map(Number);
  return round === "down" ? hours : minutes > 0 ? hours + 1 : hours;
}

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

  // The same hours the agent books against — see BusinessHoursCard. A calendar that disagrees
  // with them offers staff a slot the phone line would refuse, which is the worst kind of
  // disagreement: it only shows up as a double booking days later.
  const hoursState = useApiData(() => getBusinessHours(token!), [token]);
  const businessHours = hoursState.status === "success" ? hoursState.data : [];

  const openDays = new Set(
    businessHours.filter((d) => d.opensAt !== null && d.closesAt !== null).map((d) => d.dayOfWeek),
  );
  const isOpen = (day: Date) => openDays.size === 0 || openDays.has(DAY_NAMES[day.getDay()]);

  const openings = businessHours.filter((d) => d.opensAt !== null && d.closesAt !== null);
  const gridHours =
    openings.length === 0
      ? FALLBACK_HOURS
      : (() => {
          // The widest window any open day keeps. One pair of hours is what Administration sets,
          // but the storage is still per day and a week edited before that change can hold
          // several — the grid has to contain all of them or it hides appointments.
          const first = Math.min(...openings.map((d) => hourOf(d.opensAt!, "down")));
          const last = Math.max(...openings.map((d) => hourOf(d.closesAt!, "up")));
          return Array.from({ length: Math.max(1, last - first) }, (_, i) => first + i);
        })();

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
          isOpen={isOpen}
          onDayClick={(day) => {
            setAnchorDate(day);
            setView("day");
          }}
        />
      ) : (
        <TimeGridCalendar
          days={view === "day" ? [dayStart] : Array.from({ length: 7 }, (_, i) => new Date(weekStart.getTime() + i * 86_400_000))}
          hours={gridHours}
          isOpen={isOpen}
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
