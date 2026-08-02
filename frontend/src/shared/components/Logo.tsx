/** The sekretar.az mark and wordmark.
 *
 * Drawn rather than shipped as an image file, for the same reason as the Lamiya avatar: it is
 * built on the accent token, so it stays correct across all five themes instead of being a
 * fixed-colour asset that looks pasted on in four of them.
 *
 * The mark is a handset in a rounded square — the same glyph the dial button on the Call page
 * uses, so the logo and the thing you press to start a call are recognisably one product. It
 * has to survive being rendered at 16px in a browser tab, which rules out anything finer.
 *
 * Keep in step with `public/favicon.svg`, which is this mark with the accent colour resolved
 * to a literal: a favicon has no CSS custom properties to read.
 */
export function Logo({ size = 30, withWordmark = true }: { size?: number; withWordmark?: boolean }) {
  return (
    <span className="logo" aria-label="sekretar.az" role="img">
      <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
        <rect width="32" height="32" rx="9" fill="var(--color-accent)" />
        <path
          transform="translate(6.4 6.4) scale(0.8)"
          fill="var(--color-accent-ink)"
          d="M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z"
        />
      </svg>
      {withWordmark && (
        <span className="logo-text" aria-hidden="true">
          sekretar<span className="logo-tld">.az</span>
        </span>
      )}
    </span>
  );
}
