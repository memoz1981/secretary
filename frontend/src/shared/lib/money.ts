// Agent spend spans four orders of magnitude — a month's bill is tens of dollars, a single
// answer costs under two cents — so one fixed number of decimal places is wrong at one end or
// the other. Two decimals would round every per-answer figure to $0.02; four would clutter a
// total with digits nobody reads.

/** USD, with as many decimals as the size of the number warrants. Null renders as an em dash
 *  rather than $0.00, because "no meaningful rate" and "free" are different facts. */
export function formatUsd(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  const magnitude = Math.abs(value);
  if (magnitude >= 1) return `$${value.toFixed(2)}`;
  if (magnitude >= 0.01) return `$${value.toFixed(3)}`;
  if (magnitude === 0) return "$0.00";
  return `$${value.toFixed(4)}`;
}

/** Thousands-separated token counts — six-figure numbers are unreadable without them. */
export function formatTokens(value: number): string {
  return value.toLocaleString();
}

export function formatDuration(seconds: number): string {
  return `${Math.floor(seconds / 60)}m ${Math.round(seconds % 60)}s`;
}
