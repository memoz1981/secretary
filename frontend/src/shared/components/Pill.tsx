import type { ReactNode } from "react";

type PillVariant = "success" | "warning" | "critical" | "neutral";

/** A badge. `variant` says what it means — success, warning, critical — and `tone` says what it
 *  *is*: one of the eight module colours the landing tiles and the picker already use, so a
 *  module looks the same everywhere a customer meets it. A tone wins over the variant. */
export function Pill({
  variant = "neutral",
  tone,
  children,
}: {
  variant?: PillVariant;
  tone?: number;
  children: ReactNode;
}) {
  return <span className={`pill ${tone ? `tone-${tone}` : variant}`}>{children}</span>;
}
