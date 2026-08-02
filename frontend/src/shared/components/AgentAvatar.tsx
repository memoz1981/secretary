/** Lamiya, drawn rather than photographed.
 *
 * Inline SVG on purpose: it inherits the active theme's accent tokens, so the avatar stays
 * legible on all five palettes instead of being a fixed-color asset that looks pasted on in
 * four of them. It also keeps the app free of binary assets — `public/` holds only web.config.
 */
export function AgentAvatar({ size = 104, className = "agent-avatar" }: { size?: number; className?: string }) {
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 96 96"
      role="img"
      aria-label="Lamiya"
      style={{ width: size, height: size }}
    >
      <circle cx="48" cy="48" r="48" fill="var(--color-accent-subtle)" />
      {/* Shoulders first, so the head overlaps them and reads as a bust rather than a
          floating circle. The arc tops out at y=56, exactly where the head ends. */}
      <path d="M26 78a22 22 0 0 1 44 0z" fill="var(--color-accent)" />
      <circle cx="48" cy="42" r="14" fill="var(--color-accent)" />
      {/* Headset — the one detail that says "answers the phone" without a caption. */}
      <path
        d="M30 42a18 18 0 0 1 36 0"
        fill="none"
        stroke="var(--color-accent)"
        strokeWidth="3.5"
        strokeLinecap="round"
      />
      <rect x="25.5" y="39" width="7.5" height="13" rx="3.75" fill="var(--color-accent)" />
      <rect x="63" y="39" width="7.5" height="13" rx="3.75" fill="var(--color-accent)" />
      <path
        d="M66.5 52.5v3a6 6 0 0 1-6 6h-1.5"
        fill="none"
        stroke="var(--color-accent)"
        strokeWidth="2.75"
        strokeLinecap="round"
      />
      <circle cx="57.5" cy="61.5" r="2.75" fill="var(--color-accent)" />
    </svg>
  );
}
