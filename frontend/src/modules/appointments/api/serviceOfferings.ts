import { apiFetch } from "@/shared/api/client";
import type { CreateServiceOfferingRequest, ServiceOfferingResponse, UpdateServiceOfferingRequest } from "@/shared/api/types";

export function getServiceOfferings(token: string) {
  return apiFetch<ServiceOfferingResponse[]>("/api/service-offerings", { token });
}

export function createServiceOffering(token: string, request: CreateServiceOfferingRequest) {
  return apiFetch<ServiceOfferingResponse>("/api/service-offerings", { method: "POST", body: request, token });
}

export function updateServiceOffering(token: string, id: number, request: UpdateServiceOfferingRequest) {
  return apiFetch<ServiceOfferingResponse>(`/api/service-offerings/${id}`, { method: "PUT", body: request, token });
}

export function removeServiceOffering(token: string, id: number) {
  return apiFetch<void>(`/api/service-offerings/${id}`, { method: "DELETE", token });
}
