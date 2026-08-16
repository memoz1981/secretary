/** Shares of a whole, as a pie — and only when there are three of them or fewer.
 *
 * ⚠ The cap is not taste. A pie is an all-pairs form: every slice sits against every other, so
 * every pair has to be separable, including for a reader with colour-vision deficiency. Running
 * candidate palettes through a CVD check, three is where it stops — a fourth hue lands within
 * ΔE 8 of one already there, and a six-slice set had a worst pair at ΔE 0.5, which is to say
 * identical. Callers use {@link ShareBars} above three; the two are meant to look like siblings.
 *
 * Every slice is labelled with its own name and percentage, so identity never rests on colour. */
export interface Share {
  label: string;
  count: number;
}

const COLORS = ["var(--chart-1)", "var(--chart-2)", "var(--chart-3)"];

/** The most slices a pie can hold and still be readable by everyone. */
export const MAX_PIE_SLICES = COLORS.length;

export function SharePie({ shares, size = 132 }: { shares: Share[]; size?: number }) {
  const total = shares.reduce((sum, s) => sum + s.count, 0);
  if (total === 0) {
    return null;
  }

  const radius = size / 2;
  let angle = -Math.PI / 2; // Twelve o'clock, where a reader starts.

  const wedges = shares.map((share, index) => {
    const sweep = (share.count / total) * Math.PI * 2;
    const from = angle;
    angle += sweep;

    return {
      ...share,
      color: COLORS[index % COLORS.length],
      percent: Math.round((share.count / total) * 100),
      // A single share of everything has no arc to draw — two identical points make an empty
      // path rather than a full circle, so it is drawn as one.
      path:
        share.count === total
          ? `M ${radius} ${radius} m -${radius} 0 a ${radius} ${radius} 0 1 0 ${size} 0 `
            + `a ${radius} ${radius} 0 1 0 -${size} 0`
          : `M ${radius} ${radius} L ${radius + radius * Math.cos(from)} ${radius + radius * Math.sin(from)} `
            + `A ${radius} ${radius} 0 ${sweep > Math.PI ? 1 : 0} 1 `
            + `${radius + radius * Math.cos(angle)} ${radius + radius * Math.sin(angle)} Z`,
    };
  });

  return (
    <div className="share-pie">
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="presentation">
        {wedges.map((w) => (
          // A 2px stroke in the surface colour, so neighbouring slices are separated by the page
          // rather than by a line of their own — the same spacer the bars use.
          <path key={w.label} d={w.path} fill={w.color} stroke="var(--color-surface)" strokeWidth="2" />
        ))}
      </svg>
      <ul className="share-legend">
        {wedges.map((w) => (
          <li key={w.label}>
            <span className="share-swatch" style={{ background: w.color }} aria-hidden="true" />
            <span className="share-label">{w.label}</span>
            <span className="share-value mono">
              {w.percent}% · {w.count}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** The same shares as bars, for when there are too many to colour apart.
 *
 * One hue for every bar: the reader compares lengths, and giving each bar its own colour would
 * encode the label twice — once in the text beside it and once in a hue that means nothing. */
export function ShareBars({ shares }: { shares: Share[] }) {
  const total = shares.reduce((sum, s) => sum + s.count, 0);
  const widest = Math.max(1, ...shares.map((s) => s.count));

  return (
    <ul className="share-bars">
      {shares.map((share) => (
        <li key={share.label}>
          <span className="share-label">{share.label}</span>
          <span className="share-track">
            <span
              className="share-fill"
              style={{ width: `${(share.count / widest) * 100}%` }}
              aria-hidden="true"
            />
          </span>
          <span className="share-value mono">
            {total === 0 ? 0 : Math.round((share.count / total) * 100)}% · {share.count}
          </span>
        </li>
      ))}
    </ul>
  );
}
