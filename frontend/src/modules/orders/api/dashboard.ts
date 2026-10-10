import { apiFetch, toQueryString } from "@/shared/api/client";
import type { OrderDashboardResponse } from "@/shared/api/types";

export function getOrderDashboard(token: string, from: string, to: string) {
  return apiFetch<OrderDashboardResponse>(`/api/orders/dashboard${toQueryString({ from, to })}`, { token });
}
