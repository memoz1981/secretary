import { apiFetch, toQueryString } from "@/shared/api/client";
import type {
  AccountResponse,
  CreateTenantRequest,
  CreateTenantResult,
  Module,
  TenantModuleResponse,
  TenantInsightsResponse,
  TenantResponse,
  UpdateOwnTenantRequest,
  UpdateTenantRequest,
} from "@/shared/api/types";

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

/** Every module with whether this tenant holds it — the full set, not only what was granted
 *  before, so the screen can render a switch per module. */
export function getTenantModules(token: string, id: number) {
  return apiFetch<TenantModuleResponse[]>(`/api/tenants/${id}/modules`, { token });
}

export function setTenantModule(token: string, id: number, module: Module, enabled: boolean) {
  return apiFetch<TenantModuleResponse>(`/api/tenants/${id}/modules`, {
    method: "PUT",
    body: { module, enabled },
    token,
  });
}

// ---- Tenant self-service (the business's own Admin page, no id needed) ----

/** Every module and whether this tenant holds it. The full catalogue, so the picker can show a
 *  business what exists beside what they bought. */
export function getMyModules(token: string) {
  return apiFetch<TenantModuleResponse[]>("/api/tenant/modules", { token });
}

export function getCurrentTenant(token: string) {
  return apiFetch<TenantResponse>("/api/tenant", { token });
}

export function updateCurrentTenant(token: string, request: UpdateOwnTenantRequest) {
  return apiFetch<TenantResponse>("/api/tenant", { method: "PUT", body: request, token });
}

/** What a tenant has used, across every module they hold. Platform admin only. */
export function getTenantInsights(token: string, id: number) {
  return apiFetch<TenantInsightsResponse>(`/api/tenants/${id}/insights`, { token });
}
