import type { ReactNode } from "react";

/** Card icons for the public page.
 *
 * Inline SVG on theme tokens — coloured from the eight `--provider-*` hues, which already
 * exist for the calendar and are the only palette in this system defined separately for light
 * and dark themes. That is what lets the page carry colour without shipping an image file
 * that would look wrong on four of the five themes.
 */

const ICONS: Record<string, ReactNode> = {
  phoneIn: (
    <>
      <path d="M15.5 3.5v5h5" />
      <path d="M21 3l-5.5 5.5" />
      <path d="M4.5 5h3.2l1.4 3.5-2 1.3a11.5 11.5 0 0 0 5.1 5.1l1.3-2 3.5 1.4v3.2a1.5 1.5 0 0 1-1.6 1.5A15.5 15.5 0 0 1 3 6.6 1.5 1.5 0 0 1 4.5 5z" />
    </>
  ),
  /** A handset with an arrow each way. phoneIn draws one arrow pointing in, which was right when
   *  the first step said "a customer calls" and wrong the moment it also meant the agent ringing
   *  out — an icon that contradicts the sentence beside it is read before the sentence is. */
  phoneBoth: (
    <>
      <path d="M4.5 5h3.2l1.4 3.5-2 1.3a11.5 11.5 0 0 0 5.1 5.1l1.3-2 3.5 1.4v3.2a1.5 1.5 0 0 1-1.6 1.5A15.5 15.5 0 0 1 3 6.6 1.5 1.5 0 0 1 4.5 5z" />
      <path d="M14 3.5h6.5V10" />
      <path d="M20.5 3.5L15 9" />
      <path d="M21 8.5v-5h-5" />
    </>
  ),
  speak: (
    <>
      <rect x="3" y="3.5" width="18" height="13" rx="3.5" />
      <path d="M8 16.5v4l4-4" />
      <path d="M8.5 8v4M12 6.5v7M15.5 9v2" />
    </>
  ),
  calendarCheck: (
    <>
      <rect x="3" y="5" width="18" height="16" rx="2.5" />
      <path d="M3 10h18M8 3v4M16 3v4" />
      <path d="M9 15.5l2 2 4-4" />
    </>
  ),
  info: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5M12 7.6h.01" />
    </>
  ),
  bell: (
    <>
      <path d="M6 9a6 6 0 0 1 12 0c0 5 2 6 2 6H4s2-1 2-6z" />
      <path d="M10 19a2 2 0 0 0 4 0" />
    </>
  ),
  star: <path d="M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8-5.2-2.7-5.2 2.7 1-5.8L3.5 9.7l5.9-.9z" />,
  bag: (
    <>
      <path d="M6 8h12l-1 12H7z" />
      <path d="M9 8V6a3 3 0 0 1 6 0v2" />
    </>
  ),
  clipboard: (
    <>
      <rect x="5" y="4" width="14" height="17" rx="2" />
      <rect x="9" y="2.5" width="6" height="3.5" rx="1.2" />
      <path d="M9 11.5h6M9 15.5h4" />
    </>
  ),
  globe: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M3 12h18" />
      <path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z" />
    </>
  ),
  clock: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3.5 2" />
    </>
  ),
  transcript: (
    <>
      <path d="M6 3h7l5 5v12a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z" />
      <path d="M13 3v5h5" />
      <path d="M8.5 13h7M8.5 17h4" />
    </>
  ),
  gauge: (
    <>
      <path d="M4 17a8 8 0 1 1 16 0" />
      <path d="M12 17l4-5" />
    </>
  ),
  handoff: (
    <>
      <circle cx="9" cy="8" r="3.2" />
      <path d="M3.5 19.5a5.5 5.5 0 0 1 11 0" />
      <path d="M16.5 9h4.5m0 0l-2-2m2 2l-2 2" />
    </>
  ),
  sync: (
    <>
      <path d="M4 12a8 8 0 0 1 13.7-5.7L20 8" />
      <path d="M20 4v4h-4" />
      <path d="M20 12a8 8 0 0 1-13.7 5.7L4 16" />
      <path d="M4 20v-4h4" />
    </>
  ),
};

export type IconName = keyof typeof ICONS;

/** A coloured tile with an icon. `tone` picks one of the eight provider hues (1-8). */
export function IconChip({ name, tone }: { name: IconName; tone: number }) {
  return (
    <span className={`icon-chip tone-${tone}`}>
      <svg
        viewBox="0 0 24 24"
        width="22"
        height="22"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
        focusable="false"
      >
        {ICONS[name]}
      </svg>
    </span>
  );
}

