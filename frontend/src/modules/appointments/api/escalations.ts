import { apiFetch } from "@/shared/api/client";
import type { EscalationResponse } from "@/shared/api/types";

export function getRingingEscalations(token: string) {
  return apiFetch<EscalationResponse[]>("/api/escalations/ringing", { token });
}

export function acceptEscalation(token: string, id: number) {
  return apiFetch<EscalationResponse>(`/api/escalations/${id}/accept`, { method: "POST", token });
}

export function endEscalationCall(token: string, id: number) {
  return apiFetch<void>(`/api/escalations/${id}/end`, { method: "POST", token });
}
