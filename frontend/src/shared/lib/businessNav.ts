import type { NavItem } from "@/shared/components/AppShell";
import type { AccountRole } from "@/shared/api/types";
import type { TranslationKey } from "@/shared/i18n/translations";
import type { ModuleManifest } from "@/modules/registry";

/** The sidebar: the module you are working in, then the tenant-level pages.
 *
 * Clients, the Call Log and the Dashboard are deliberately not per-module. A tenant has one
 * customer list and one phone line however many modules they hold, and splitting those would
 * mean three copies of the same person.
 *
 * `activeModule` is resolved by useActiveModule rather than read off the URL here, because the
 * tenant-level pages sit outside every module's path — deriving it from the path alone blanked
 * the sidebar as soon as one of them was opened. */
export function businessNavItems(
  role: AccountRole | null,
  t: (key: TranslationKey) => string,
  activeModule: ModuleManifest | null,
  moduleCount: number,
): NavItem[] {
  // No module, nothing to work in. The only screen a tenant in that state should see is the
  // notice explaining it, and offering the tenant-level links there was a way round it.
  if (!activeModule) {
    return [];
  }

  const items: NavItem[] = [...activeModule.nav(role, t)];

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

  // Only worth offering when there is somewhere else to go. Without it the only way back to the
  // picker was to log out and in again.
  if (moduleCount > 1) {
    items.push({ label: t("navSwitchModule"), to: "/app" });
  }

  return items;
}
