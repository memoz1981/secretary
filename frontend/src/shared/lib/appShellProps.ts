import { useLocation } from "react-router-dom";
import { useAuth } from "@/shared/auth/AuthContext";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { accountRoleLabels, translateEnum } from "@/shared/i18n/translations";
import { businessNavItems } from "@/shared/lib/businessNav";

/** Shared AppShell props for the tenant/business pages (Calendar, Services, Team, Call Log,
 * Call Detail, Dashboard) — identical across all six, so it's factored out once rather than
 * repeated. Falls back to just the role label until GET /api/auth/me resolves. */
export function useBusinessShell() {
  const { role, me } = useAuth();
  const { language, t } = useLanguage();
  const { pathname } = useLocation();
  const roleLabel = role ? translateEnum(accountRoleLabels, role, language) : "";

  return {
    brand: me?.tenantName ?? t("brandGeneric"),
    domainLabel: roleLabel,
    whoText: me ? `${me.name} · ${roleLabel}` : roleLabel,
    // Which module's pages appear comes from the URL — see businessNavItems.
    navItems: businessNavItems(role, t, pathname, me?.enabledModules ?? []),
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
