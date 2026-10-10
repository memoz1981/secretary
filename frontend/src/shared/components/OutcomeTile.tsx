/** One outcome: how many calls ended that way, and what share of the range that was.
 *
 * ⚠ The share is handed in, never computed here. Five tiles each dividing by a total they were
 * given separately is five chances to divide by a different number, and the first symptom is
 * percentages that do not add to 100 on a customer's screen. The server counts and divides once.
 *
 * The tone is the reading, not decoration: green did its job, amber needed a person, red did not
 * finish, and the total is neutral because it is not a verdict on anything. */
export type OutcomeTone = "total" | "good" | "warn" | "bad";

const ACCENT: Record<OutcomeTone, string> = {
  total: "var(--color-accent)",
  good: "var(--color-success)",
  warn: "var(--color-warning)",
  bad: "var(--color-critical)",
};

export function OutcomeTile({
  label,
  count,
  share,
  tone,
  onClick,
}: {
  label: string;
  count: number;
  /** 0–1. Omitted for the total, which is always the whole and would only read as noise. */
  share?: number;
  tone: OutcomeTone;
  onClick?: () => void;
}) {
  const accent = ACCENT[tone];

  return (
    <button
      type="button"
      className="outcome-tile"
      onClick={onClick}
      disabled={onClick === undefined}
      style={{ borderTopColor: accent }}
    >
      <span className="outcome-share mono" style={{ color: accent }}>
        {share === undefined ? "100%" : `${Math.round(share * 100)}%`}
      </span>
      <span className="outcome-label">{label}</span>
      <span className="outcome-count" style={{ color: accent }}>
        {count}
      </span>
    </button>
  );
}
