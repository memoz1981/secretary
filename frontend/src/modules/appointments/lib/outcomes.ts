import type { OutcomeTone } from "@/shared/components/OutcomeTile";
import type { AppointmentCallOutcome } from "@/shared/api/types";

/** What each outcome means, as a colour — in one place.
 *
 * ⚠ This rule was written out three times: once for the dashboard tiles, once for the pill in the
 * call log, once for the pill on the call detail. Three copies of one judgement is three chances
 * for a call to be amber in the list and red on its own page, and a reader who notices that stops
 * believing either.
 *
 * Green did its job. Amber needed something we could not give on the call — a person, or a free
 * slot. Red reached somebody and did not finish.
 *
 * NoAvailability is amber rather than red on purpose: it is not a fault. A week of them is a
 * staffing decision, and painting it as a failure argues for fixing an agent that did nothing
 * wrong. */
export const OUTCOME_TONE: Record<AppointmentCallOutcome, OutcomeTone> = {
  Resolved: "good",
  Forwarded: "warn",
  Unfinished: "bad",
  NoAvailability: "warn",
};

const PILL: Record<OutcomeTone, "success" | "warning" | "critical" | "neutral"> = {
  total: "neutral",
  good: "success",
  warn: "warning",
  bad: "critical",
};

/** The same reading as the tile, in the vocabulary a Pill speaks. */
export function outcomePillVariant(outcome: AppointmentCallOutcome) {
  return PILL[OUTCOME_TONE[outcome]];
}
