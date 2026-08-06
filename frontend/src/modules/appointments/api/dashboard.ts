import { apiFetch, toQueryString } from "@/shared/api/client";
import type { DashboardSummaryResponse } from "@/shared/api/types";

export function getDashboardSummary(token: string, from: string, to: string) {
  return apiFetch<DashboardSummaryResponse>(`/api/dashboard/summary${toQueryString({ from, to })}`, { token });
}
