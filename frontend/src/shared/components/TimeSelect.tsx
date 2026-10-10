/** A time of day, on the half hour, always written 24-hour.
 *
 * ⚠ A select rather than <input type="time">, and the reason is the display format. That input
 * renders its value in the browser's own locale, so an English-locale browser showed Azerbaijani
 * opening hours as "09:00 PM" — a clock face nobody here reads. The `lang` attribute does not
 * reliably override it; Chrome follows the browser's language setting, not the page's. A select
 * is the only way to be certain what the reader sees.
 *
 * Half-hour steps because that is what the booking grid offers: CheckAvailability answers with
 * "any start on the hour or half hour", so an opening time of 09:15 would describe a business
 * whose first bookable slot is 09:30 anyway. */
const STEPS = Array.from({ length: 48 }, (_, i) => {
  const hours = String(Math.floor(i / 2)).padStart(2, "0");
  const minutes = i % 2 === 0 ? "00" : "30";
  return `${hours}:${minutes}`;
});

export function TimeSelect({
  value,
  disabled,
  onChange,
}: {
  value: string;
  disabled?: boolean;
  onChange: (next: string) => void;
}) {
  // A stored time that is not on the half hour still has to be selectable, or opening the card
  // would silently round somebody's 09:15 to 09:00 the moment they touched anything else.
  const options = STEPS.includes(value) ? STEPS : [value, ...STEPS].sort();

  return (
    <select className="mono" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
      {options.map((time) => (
        <option key={time} value={time}>
          {time}
        </option>
      ))}
    </select>
  );
}
