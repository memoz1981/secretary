import { useLocation } from "react-router-dom";
import { useAuth } from "@/shared/auth/AuthContext";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { businessNavItems } from "@/shared/lib/businessNav";
import { useActiveModule } from "@/modules/useActiveModule";
import { usableModules } from "@/modules/registry";

/** Shared AppShell props for every tenant-facing page — identical across all of them, so it is
 * factored out once rather than repeated. */
export function useBusinessShell() {
  const { role, me } = useAuth();
  const { t } = useLanguage();
  const { pathname } = useLocation();
  const activeModule = useActiveModule();
  const moduleCount = usableModules(me?.enabledModules ?? []).length;

  // The picker gets no sidebar. You are choosing where to work, not working — and leaving the
  // links there was a way straight past the choice: from the picker you could open Appointments
  // without picking it, and from inside Information reach appointment pages the same way.
  //
  // Exactly "/app", not startsWith: "/appointments/calendar" starts with "/app", so the whole
  // Appointment module was treated as the picker and rendered with no sidebar at all. Nothing
  // is nested under the picker, so an equality check is also the honest description of it.
  const choosing = pathname === "/app";

  return {
    brand: me?.tenantName ?? t("brandGeneric"),
    // The module, or nothing. It used to fall back to the role, which is how a tenant holding
    // no modules ended up with "OWNER" as the only thing under their name — a label for a
    // sidebar that had nothing in it.
    domainLabel: !choosing && activeModule ? t(activeModule.titleKey) : "",
    // Just who they are. The role was on screen twice and told them nothing they did not know.
    whoText: me?.name ?? "",
    navItems: choosing ? [] : businessNavItems(role, t, activeModule, moduleCount),
  };
}

/** Same idea for the platform-admin pages (Tenant List/Create/Detail) — a fixed single-item
 * nav, so no need to thread `role` through. */
export function usePlatformAdminShell() {
  const { me } = useAuth();
  const { t } = useLanguage();

  return {
    brand: t("brandPlatform"),
    domainLabel: t("rolePlatformAdmin"),
    whoText: me ? `${me.name} · ${t("rolePlatformAdmin")}` : t("signedInAsProductOwner"),
    navItems: [{ label: t("navTenants"), to: "/admin/tenants" }],
  };
}
