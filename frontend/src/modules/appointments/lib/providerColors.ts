import type { ProviderResponse } from "@/shared/api/types";

/** One visual identity per provider on the calendar: a strong edge color and a soft fill.
 * The actual colors are CSS tokens (`--provider-N-*` in tokens.css) so each theme can tune
 * them — a dark theme needs dark fills with bright edges or the appointment text becomes
 * unreadable. Eight hues, spaced far apart; the palette wraps after eight. */
export interface ProviderColor {
  border: string;
  bg: string;
}

const PALETTE_SIZE = 8;

/** Stable color lookup keyed on the provider's position in the (name-ordered) provider
 * list, so a provider keeps its color across views and filter changes. */
export function makeProviderColorLookup(providers: ProviderResponse[]): (providerId: number) => ProviderColor {
  const indexById = new Map(providers.map((p, i) => [p.id, i]));
  return (providerId) => {
    const slot = ((indexById.get(providerId) ?? 0) % PALETTE_SIZE) + 1;
    return { border: `var(--provider-${slot}-border)`, bg: `var(--provider-${slot}-bg)` };
  };
}
