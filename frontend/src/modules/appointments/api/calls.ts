import { apiFetch, toQueryString } from "@/shared/api/client";
import type {
  AppointmentCallOutcome,
  CallClassification,
  CallDetailResponse,
  CallPipeline,
  CallResponse,
} from "@/shared/api/types";

export interface CallSearchParams {
  from?: string;
  to?: string;
  classification?: CallClassification;
  outcome?: AppointmentCallOutcome;
  providerId?: number;
  pipeline?: CallPipeline;
}

export function searchCalls(token: string, params: CallSearchParams) {
  return apiFetch<CallResponse[]>(`/api/calls${toQueryString({ ...params })}`, { token });
}

export function getCallDetail(token: string, id: number) {
  return apiFetch<CallDetailResponse>(`/api/calls/${id}`, { token });
}
