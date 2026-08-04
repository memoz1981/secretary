import type { NavItem } from "@/shared/components/AppShell";
import type { AccountRole, Module } from "@/shared/api/types";
import type { TranslationKey } from "@/shared/i18n/translations";
import { MODULE_REGISTRY, usableModules } from "@/modules/registry";

/** The sidebar: the current module's own pages, then the tenant-level ones.
 *
 * Which module is "current" comes from the URL rather than from state, so a deep link, a
 * refresh and the back button all agree without anything having to be kept in sync.
 *
 * Clients, the Call Log and the Dashboard are deliberately not per-module. A tenant has one
 * customer list and one phone line however many modules they hold, and splitting those would
 * mean three copies of the same person. Only what a module genuinely owns sits above them. */
export function businessNavItems(
  role: AccountRole | null,
  t: (key: TranslationKey) => string,
  pathname: string,
  enabledModules: Module[],
): NavItem[] {
  // Held as well as matched. Without the second check, landing on a module's URL — typed, or
  // from a bookmark predating a revocation — built a sidebar full of that module's links for a
  // tenant who no longer has it.
  const active = MODULE_REGISTRY.find(
    (m) => pathname.startsWith(m.pathPrefix) && enabledModules.includes(m.key),
  );
  const items: NavItem[] = active ? [...active.nav(role, t)] : [];

  items.push({ label: t("navClients"), to: "/clients" });
  if (role === "Owner") {
    // Demo live call to the AI agent — Owner only, matching the /voice/live-call policy.
    items.push({ label: t("navCall"), to: "/call" });
  }
  items.push({ label: t("navCallLog"), to: "/calls" });
  items.push({ label: t("navDashboard"), to: "/dashboard" });
  if (role === "Owner") {
    // Tenant self-service: business details + accounts.
    items.push({ label: t("navAdmin"), to: "/admin" });
  }

  // Only worth offering when there is somewhere else to go. A tenant with one module should
  // never meet the concept at all.
  if (usableModules(enabledModules).length > 1) {
    items.push({ label: t("navSwitchModule"), to: "/app" });
  }

  return items;
}
