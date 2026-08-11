import { apiFetch, toQueryString } from "@/shared/api/client";
import type { CallOutcome, OrderCallResponse } from "@/shared/api/types";

export interface OrderCallSearchParams {
  from?: string;
  to?: string;
  outcome?: CallOutcome;
}

export function searchOrderCalls(token: string, params: OrderCallSearchParams) {
  return apiFetch<OrderCallResponse[]>(`/api/orders/calls${toQueryString({ ...params })}`, { token });
}
