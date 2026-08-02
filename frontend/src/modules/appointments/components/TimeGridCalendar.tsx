import type { AppointmentResponse } from "@/shared/api/types";
import type { ProviderColor } from "@/modules/appointments/lib/providerColors";
import { useLanguage } from "@/shared/i18n/LanguageContext";

interface TimeGridCalendarProps {
  days: Date[];
  hours: number[];
  appointments: AppointmentResponse[];
  serviceName: (serviceOfferingId: number) => string;
  providerColor: (providerId: number) => ProviderColor;
  onApptClick: (appt: AppointmentResponse) => void;
}

const DAY_LABEL_KEYS = ["dayMon", "dayTue", "dayWed", "dayThu", "dayFri", "daySat", "daySun"] as const;

/** Pixels per hour — 32px per half-hour row keeps a 30-minute block tall enough to read. */
const HOUR_PX = 64;

interface PositionedAppointment {
  appt: AppointmentResponse;
  /** Display interval in minutes from the top of the grid, snapped to half-hour cells. */
  startMin: number;
  endMin: number;
  /** Column assignment inside an overlap cluster: this block renders in column `col` of `cols`. */
  col: number;
  cols: number;
}

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

/** Positions one day's appointments: snap each to the half-hour cells it touches (start
 * floored, end ceiled, 30 min minimum), then split genuinely overlapping blocks into
 * side-by-side columns — the classic calendar cluster layout — so two clients at the same
 * hour render in parallel instead of stacked at :00 and :30. */
function layoutDay(
  appointments: AppointmentResponse[],
  day: Date,
  firstHour: number,
  totalMinutes: number,
): PositionedAppointment[] {
  const items: PositionedAppointment[] = appointments
    .filter((a) => a.status !== "Cancelled" && isSameDay(new Date(a.start), day))
    .map((a) => {
      const start = new Date(a.start);
      const end = new Date(a.end);
      const rawStart = start.getHours() * 60 + start.getMinutes() - firstHour * 60;
      const rawEnd = rawStart + Math.max(1, (end.getTime() - start.getTime()) / 60_000);

      const startMin = Math.max(0, Math.floor(rawStart / 30) * 30);
      const endMin = Math.min(totalMinutes, Math.max(startMin + 30, Math.ceil(rawEnd / 30) * 30));
      return { appt: a, startMin, endMin, col: 0, cols: 1 };
    })
    .filter((p) => p.startMin < totalMinutes && p.endMin > 0)
    .sort((a, b) => a.startMin - b.startMin || b.endMin - a.endMin);

  // Group into clusters of transitively-overlapping blocks, then greedily assign each block
  // to the first column that is free at its start time.
  let cluster: PositionedAppointment[] = [];
  let clusterEnd = -1;
  let columnEnds: number[] = [];

  function closeCluster() {
    for (const item of cluster) {
      item.cols = columnEnds.length;
    }
    cluster = [];
    columnEnds = [];
  }

  for (const item of items) {
    if (cluster.length > 0 && item.startMin >= clusterEnd) {
      closeCluster();
    }

    const freeColumn = columnEnds.findIndex((end) => end <= item.startMin);
    if (freeColumn >= 0) {
      item.col = freeColumn;
      columnEnds[freeColumn] = item.endMin;
    } else {
      item.col = columnEnds.length;
      columnEnds.push(item.endMin);
    }

    cluster.push(item);
    clusterEnd = Math.max(clusterEnd, item.endMin);
  }

  closeCluster();
  return items;
}

/** Shared time grid for both the Day view (a single day) and the Week view (7 days).
 * Appointments are absolutely positioned blocks: vertical extent = duration rounded to the
 * half-hour cells it covers, horizontal position = overlap column, color = provider. */
export function TimeGridCalendar({ days, hours, appointments, serviceName, providerColor, onApptClick }: TimeGridCalendarProps) {
  const { t } = useLanguage();
  const firstHour = hours[0];
  const totalMinutes = hours.length * 60;
  const totalHeight = (totalMinutes / 60) * HOUR_PX;

  return (
    <div className="week-grid" style={{ gridTemplateColumns: `56px repeat(${days.length}, 1fr)` }}>
      <div className="head" />
      {days.map((d, i) => (
        <div className="head" key={i}>
          {t(DAY_LABEL_KEYS[(d.getDay() + 6) % 7])} {d.getDate()}
        </div>
      ))}

      <div className="timecol" style={{ height: totalHeight }}>
        {hours.map((hour) => (
          <div className="time" key={hour} style={{ height: HOUR_PX }}>
            {String(hour).padStart(2, "0")}:00
          </div>
        ))}
      </div>

      {days.map((day, dayIndex) => (
        <div className="day-col" key={dayIndex} style={{ height: totalHeight }}>
          {layoutDay(appointments, day, firstHour, totalMinutes).map((p) => {
            const color = providerColor(p.appt.providerId);
            const width = 100 / p.cols;
            return (
              <div
                key={p.appt.id}
                className={`appt abs ${p.appt.reminderNoAnswerFlag ? "flag" : ""}`}
                style={{
                  top: (p.startMin / 60) * HOUR_PX + 1,
                  height: ((p.endMin - p.startMin) / 60) * HOUR_PX - 3,
                  left: `calc(${p.col * width}% + 2px)`,
                  width: `calc(${width}% - 4px)`,
                  ...(p.appt.reminderNoAnswerFlag ? {} : { background: color.bg, borderLeftColor: color.border }),
                }}
                onClick={() => onApptClick(p.appt)}
              >
                {p.appt.clientName ?? p.appt.clientPhoneNumber} — {serviceName(p.appt.serviceOfferingId)}
                {p.appt.reminderNoAnswerFlag && <div className="flagtag">{t("noAnswerReminder")}</div>}
              </div>
            );
          })}
        </div>
      ))}
    </div>
  );
}
