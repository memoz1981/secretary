import { apiFetch } from "@/shared/api/client";
import type {
  AppointmentResponse,
  BlackListClientRequest,
  ClientResponse,
  CreateClientRequest,
  UpdateClientRequest,
} from "@/shared/api/types";

export function getClients(token: string) {
  return apiFetch<ClientResponse[]>("/api/clients", { token });
}

export function createClient(token: string, request: CreateClientRequest) {
  return apiFetch<ClientResponse>("/api/clients", { method: "POST", body: request, token });
}

export function updateClient(token: string, id: number, request: UpdateClientRequest) {
  return apiFetch<ClientResponse>(`/api/clients/${id}`, { method: "PUT", body: request, token });
}

export function removeClient(token: string, id: number) {
  return apiFetch<void>(`/api/clients/${id}`, { method: "DELETE", token });
}

export function blackListClient(token: string, id: number, request: BlackListClientRequest) {
  return apiFetch<ClientResponse>(`/api/clients/${id}/blacklist`, { method: "POST", body: request, token });
}

export function undoBlackListClient(token: string, id: number) {
  return apiFetch<ClientResponse>(`/api/clients/${id}/undo-blacklist`, { method: "POST", token });
}

export function getClientByPhone(token: string, phoneNumber: string) {
  return apiFetch<ClientResponse>(`/api/clients/by-phone/${encodeURIComponent(phoneNumber)}`, { token });
}

export function getUpcomingAppointmentsForClient(token: string, clientId: number) {
  return apiFetch<AppointmentResponse[]>(`/api/clients/${clientId}/upcoming-appointments`, { token });
}
