import type { NavItem } from "@/shared/components/AppShell";
import type { AccountRole } from "@/shared/api/types";
import type { TranslationKey } from "@/shared/i18n/translations";

export function businessNavItems(role: AccountRole | null, t: (key: TranslationKey) => string): NavItem[] {
  const items: NavItem[] = [
    { label: t("navCalendar"), to: "/calendar" },
    { label: t("navServices"), to: "/services" },
    { label: t("navProviders"), to: "/providers" },
    { label: t("navClients"), to: "/clients" },
  ];
  if (role === "Owner") {
    // Demo live call to the AI agent — Owner only, matching the /voice/live-call policy.
    items.push({ label: t("navCall"), to: "/call" });
  }
  items.push({ label: t("navCallLog"), to: "/calls" });
  items.push({ label: t("navDashboard"), to: "/dashboard" });
  if (role === "Owner") {
    // Tenant self-service: business details + accounts (the old Komanda page's account half).
    items.push({ label: t("navAdmin"), to: "/admin" });
  }
  return items;
}
