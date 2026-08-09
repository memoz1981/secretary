import { apiFetch } from "@/shared/api/client";
import type { BusinessHoursDay } from "@/shared/api/types";

export function getBusinessHours(token: string) {
  return apiFetch<BusinessHoursDay[]>("/api/business-hours", { token });
}

/** The whole week at once. A partial save would let someone close Tuesday and never notice
 *  Wednesday had never been set. */
export function updateBusinessHours(token: string, days: BusinessHoursDay[]) {
  return apiFetch<BusinessHoursDay[]>("/api/business-hours", { method: "PUT", body: { days }, token });
}
