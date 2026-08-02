import { apiFetch } from "@/shared/api/client";
import type { AccountResponse, AddStaffRequest, ResetAccountPasswordRequest, UpdateAccountRequest } from "@/shared/api/types";

export function getStaffAccounts(token: string) {
  return apiFetch<AccountResponse[]>("/api/staff-accounts", { token });
}

export function addStaffAccount(token: string, request: AddStaffRequest) {
  return apiFetch<AccountResponse>("/api/staff-accounts", { method: "POST", body: request, token });
}

export function updateStaffAccount(token: string, id: number, request: UpdateAccountRequest) {
  return apiFetch<AccountResponse>(`/api/staff-accounts/${id}`, { method: "PUT", body: request, token });
}

export function resetStaffAccountPassword(token: string, id: number, request: ResetAccountPasswordRequest) {
  return apiFetch<void>(`/api/staff-accounts/${id}/reset-password`, { method: "POST", body: request, token });
}

export function removeStaffAccount(token: string, id: number) {
  return apiFetch<void>(`/api/staff-accounts/${id}`, { method: "DELETE", token });
}
