/** Selectable color themes. Each id maps to a `:root[data-theme='<id>']` block in
 * styles/tokens.css that defines the color tokens the whole app is built on. The choice is
 * stored in localStorage and applied to <html> before first paint (see index.html), so it
 * survives reloads and new sessions on the same browser. */
export type ThemeId = "ledger" | "dark" | "forest" | "graphite" | "ocean";

/** ⚠ Change this and index.html together. The pre-paint script there cannot import from here —
 *  it runs before any module loads, which is the whole point of it — so the default lives in two
 *  places, and the two disagreeing means a flash of the wrong theme on every first visit. */
export const DEFAULT_THEME: ThemeId = "graphite";

export const THEME_IDS: ThemeId[] = ["ledger", "dark", "forest", "graphite", "ocean"];

/** Swatch shown in the Admin page picker: page background, surface, accent — repeated from
 * tokens.css on purpose, since a theme's colors can't be read from CSS until it's applied
 * and picking a palette by name alone is guesswork. Keep in sync with the token blocks. */
export const THEME_SWATCHES: Record<ThemeId, [string, string, string]> = {
  ledger: ["#f6f4f0", "#ffffff", "#2f6e63"],
  dark: ["#1c1912", "#262219", "#5fa394"],
  forest: ["#dfeee3", "#f2faf4", "#145c33"],
  graphite: ["#16181b", "#1e2125", "#e2b53f"],
  ocean: ["#dbe8f5", "#f0f7fd", "#114b7d"],
};

export function isThemeId(value: string | null): value is ThemeId {
  return value !== null && (THEME_IDS as string[]).includes(value);
}
