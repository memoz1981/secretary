import { apiFetch, toQueryString } from "@/shared/api/client";
import type {
  AppointmentResponse,
  AvailabilityResponse,
  CreateAppointmentRequest,
  RescheduleAppointmentRequest,
} from "@/shared/api/types";

export function getAppointments(token: string, from: string, to: string, providerId?: number) {
  return apiFetch<AppointmentResponse[]>(`/api/appointments${toQueryString({ from, to, providerId })}`, { token });
}

export function findAvailability(
  token: string,
  providerId: number,
  serviceOfferingId: number,
  from: string,
  to: string,
) {
  return apiFetch<AvailabilityResponse>(
    `/api/appointments/availability${toQueryString({ providerId, serviceOfferingId, from, to })}`,
    { token },
  );
}

export function createAppointment(token: string, request: CreateAppointmentRequest) {
  return apiFetch<AppointmentResponse>("/api/appointments", { method: "POST", body: request, token });
}

export function rescheduleAppointment(token: string, id: number, request: RescheduleAppointmentRequest) {
  return apiFetch<AppointmentResponse>(`/api/appointments/${id}`, { method: "PUT", body: request, token });
}

export function cancelAppointment(token: string, id: number) {
  return apiFetch<void>(`/api/appointments/${id}/cancel`, { method: "POST", token });
}
