import { apiFetch, toQueryString } from "@/shared/api/client";
import type {
  CreateProviderRequest,
  ProviderResponse,
  ProviderServiceMatrixResponse,
  SetProviderServiceAssignmentRequest,
  UpdateProviderRequest,
} from "@/shared/api/types";

export function getProviders(token: string, serviceOfferingId?: number) {
  return apiFetch<ProviderResponse[]>(`/api/providers${toQueryString({ serviceOfferingId })}`, { token });
}

export function getProviderServiceMatrix(token: string) {
  return apiFetch<ProviderServiceMatrixResponse>("/api/providers/service-matrix", { token });
}

export function setProviderServiceAssignment(token: string, providerId: number, request: SetProviderServiceAssignmentRequest) {
  return apiFetch<void>(`/api/providers/${providerId}/service-assignments`, { method: "PUT", body: request, token });
}

export function createProvider(token: string, request: CreateProviderRequest) {
  return apiFetch<ProviderResponse>("/api/providers", { method: "POST", body: request, token });
}

export function updateProvider(token: string, id: number, request: UpdateProviderRequest) {
  return apiFetch<ProviderResponse>(`/api/providers/${id}`, { method: "PUT", body: request, token });
}

export function removeProvider(token: string, id: number) {
  return apiFetch<void>(`/api/providers/${id}`, { method: "DELETE", token });
}
