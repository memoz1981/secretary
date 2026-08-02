import { apiFetch } from "@/shared/api/client";
import type { LoginRequest, LoginResponse, MeResponse } from "@/shared/api/types";

export function login(request: LoginRequest) {
  return apiFetch<LoginResponse>("/api/auth/login", { method: "POST", body: request });
}

export function getMe(token: string) {
  return apiFetch<MeResponse>("/api/auth/me", { token });
}
