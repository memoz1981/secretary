import type { NavItem } from "@/shared/components/AppShell";
import type { AccountRole } from "@/shared/api/types";
import type { TranslationKey } from "@/shared/i18n/translations";
import type { ModuleManifest } from "@/modules/registry";

/** The sidebar: the module you are working in, then the one page that is not part of any module.
 *
 * Everything except Administration is now the module's own. Clients, the call log and the
 * dashboard were tenant-level to begin with — one customer list per business seemed obviously
 * right — but the clients of an appointment line and the clients of an information line are
 * different people, so a shared list would have shown each module the other's callers.
 * Administration is genuinely shared: one company, one set of staff accounts.
 *
 * `activeModule` is resolved by useActiveModule rather than read off the URL here, because
 * Administration sits outside every module's path — deriving it from the path alone blanked the
 * sidebar as soon as that page was opened. */
export function businessNavItems(
  role: AccountRole | null,
  t: (key: TranslationKey) => string,
  activeModule: ModuleManifest | null,
  moduleCount: number,
): NavItem[] {
  // No module, nothing to work in. The only screen a tenant in that state should see is the
  // notice explaining it, and offering links there was a way round it.
  if (!activeModule) {
    return [];
  }

  const items: NavItem[] = [...activeModule.nav(role, t)];

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
