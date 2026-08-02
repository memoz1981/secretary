import type { AppointmentResponse } from "@/shared/api/types";
import type { ProviderColor } from "@/modules/appointments/lib/providerColors";
import { useLanguage } from "@/shared/i18n/LanguageContext";

interface MonthCalendarGridProps {
  monthStart: Date;
  appointments: AppointmentResponse[];
  serviceName: (serviceOfferingId: number) => string;
  providerColor: (providerId: number) => ProviderColor;
  onApptClick: (appt: AppointmentResponse) => void;
  onDayClick: (day: Date) => void;
}

const DAY_LABEL_KEYS = ["dayMon", "dayTue", "dayWed", "dayThu", "dayFri", "daySat", "daySun"] as const;

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

function buildMonthGridDays(monthStart: Date): Date[] {
  const firstOfMonth = new Date(monthStart.getFullYear(), monthStart.getMonth(), 1);
  const firstWeekday = (firstOfMonth.getDay() + 6) % 7; // Monday-first
  const gridStart = new Date(firstOfMonth);
  gridStart.setDate(gridStart.getDate() - firstWeekday);

  return Array.from({ length: 42 }, (_, i) => {
    const d = new Date(gridStart);
    d.setDate(d.getDate() + i);
    return d;
  });
}

/** Month view: a traditional 6x7 day grid. Each cell shows a compact appointment list
 * (client + service) rather than the hourly rows Day/Week use — clicking a day cell jumps
 * to that day in Day view, clicking an appointment chip opens it directly, same as elsewhere. */
export function MonthCalendarGrid({ monthStart, appointments, serviceName, providerColor, onApptClick, onDayClick }: MonthCalendarGridProps) {
  const { t } = useLanguage();
  const days = buildMonthGridDays(monthStart);
  const currentMonth = monthStart.getMonth();

  return (
    <div className="month-grid">
      {DAY_LABEL_KEYS.map((key) => (
        <div className="head" key={key}>
          {t(key)}
        </div>
      ))}
      {days.map((day, i) => {
        const dayAppointments = appointments
          .filter((a) => isSameDay(new Date(a.start), day) && a.status !== "Cancelled")
          .sort((a, b) => new Date(a.start).getTime() - new Date(b.start).getTime());
        const inCurrentMonth = day.getMonth() === currentMonth;

        return (
          <div key={i} className={`month-cell ${inCurrentMonth ? "" : "outside"}`} onClick={() => onDayClick(day)}>
            <div className="month-cell-date">{day.getDate()}</div>
            <div className="month-cell-appts">
              {dayAppointments.map((appt) => (
                <div
                  key={appt.id}
                  className={`appt compact ${appt.reminderNoAnswerFlag ? "flag" : ""}`}
                  style={
                    appt.reminderNoAnswerFlag
                      ? undefined
                      : { background: providerColor(appt.providerId).bg, borderLeftColor: providerColor(appt.providerId).border }
                  }
                  onClick={(e) => {
                    e.stopPropagation();
                    onApptClick(appt);
                  }}
                >
                  {new Date(appt.start).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}{" "}
                  {appt.clientName ?? appt.clientPhoneNumber} — {serviceName(appt.serviceOfferingId)}
                </div>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}
