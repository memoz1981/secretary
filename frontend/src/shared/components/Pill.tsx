import type { ReactNode } from "react";

type PillVariant = "success" | "warning" | "critical" | "neutral";

export function Pill({ variant = "neutral", children }: { variant?: PillVariant; children: ReactNode }) {
  return <span className={`pill ${variant}`}>{children}</span>;
}
