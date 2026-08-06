import { useAuth } from "@/shared/auth/AuthContext";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { accountRoleLabels, translateEnum } from "@/shared/i18n/translations";
import { businessNavItems } from "@/shared/lib/businessNav";
import { useActiveModule } from "@/modules/useActiveModule";
import { usableModules } from "@/modules/registry";

/** Shared AppShell props for the tenant/business pages (Calendar, Services, Team, Call Log,
 * Call Detail, Dashboard) — identical across all six, so it's factored out once rather than
 * repeated. Falls back to just the role label until GET /api/auth/me resolves. */
export function useBusinessShell() {
  const { role, me } = useAuth();
  const { language, t } = useLanguage();
  const roleLabel = role ? translateEnum(accountRoleLabels, role, language) : "";
  const activeModule = useActiveModule();
  const moduleCount = usableModules(me?.enabledModules ?? []).length;

  return {
    brand: me?.tenantName ?? t("brandGeneric"),
    // The module replaces the role here. "Owner" told the user something they already knew;
    // which module they are in is the thing that actually changes underneath them.
    domainLabel: activeModule ? t(activeModule.titleKey) : roleLabel,
    whoText: me ? `${me.name} · ${roleLabel}` : roleLabel,
    navItems: businessNavItems(role, t, activeModule, moduleCount),
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
