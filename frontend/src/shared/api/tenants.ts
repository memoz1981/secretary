import { apiFetch, toQueryString } from "@/shared/api/client";
import type { AccountResponse, CreateTenantRequest, CreateTenantResult, TenantResponse, UpdateTenantRequest } from "@/shared/api/types";

export function getTenants(token: string, search?: string) {
  return apiFetch<TenantResponse[]>(`/api/tenants${toQueryString({ search })}`, { token });
}

export function getTenant(token: string, id: number) {
  return apiFetch<TenantResponse>(`/api/tenants/${id}`, { token });
}

export function getTenantOwnerAccounts(token: string, id: number) {
  return apiFetch<AccountResponse[]>(`/api/tenants/${id}/owner-accounts`, { token });
}

export function createTenant(token: string, request: CreateTenantRequest) {
  return apiFetch<CreateTenantResult>("/api/tenants", { method: "POST", body: request, token });
}

export function updateTenant(token: string, id: number, request: UpdateTenantRequest) {
  return apiFetch<TenantResponse>(`/api/tenants/${id}`, { method: "PUT", body: request, token });
}

export function deactivateTenant(token: string, id: number) {
  return apiFetch<void>(`/api/tenants/${id}/deactivate`, { method: "POST", token });
}

export function reactivateTenant(token: string, id: number) {
  return apiFetch<void>(`/api/tenants/${id}/reactivate`, { method: "POST", token });
}

// ---- Tenant self-service (the business's own Admin page, no id needed) ----

export function getCurrentTenant(token: string) {
  return apiFetch<TenantResponse>("/api/tenant", { token });
}

export function updateCurrentTenant(token: string, request: UpdateTenantRequest) {
  return apiFetch<TenantResponse>("/api/tenant", { method: "PUT", body: request, token });
}
