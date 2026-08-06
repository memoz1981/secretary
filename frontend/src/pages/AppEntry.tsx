import { Navigate } from "react-router-dom";
import { useAuth } from "@/shared/auth/AuthContext";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { usableModules } from "@/modules/registry";
import { ModulePicker } from "@/pages/ModulePicker";

/** The one place that decides where a signed-in tenant user starts.
 *
 * Login always comes here rather than to a module, so refresh, a deep link and a return from
 * logout all behave the same way. Putting the decision in Login would duplicate it for each of
 * those, and Login already branches on role.
 *
 * A deep link straight to /appointments/calendar bypasses this entirely, which is correct. */
export function AppEntryPage() {
  const { me, role } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();

  // Platform admins hold no modules — their home is the tenant list.
  if (role === "PlatformAdmin") {
    return <Navigate to="/admin/tenants" replace />;
  }

  // /me has not landed yet. Rendering nothing beats flashing a picker that is about to
  // disappear, or redirecting somewhere we will immediately leave.
  if (!me) {
    return null;
  }

  const available = usableModules(me.enabledModules);

  if (available.length === 1) {
    // Straight through. A tenant with one module should never learn the word "module".
    return <Navigate to={available[0].home} replace />;
  }

  if (available.length > 1) {
    return <ModulePicker />;
  }

  // Either nothing granted, or everything granted is a module the front end cannot render yet.
  // Both are the platform admin's problem, not something the tenant can fix, so say so plainly
  // rather than bouncing them somewhere that will also be empty.
  return (
    <AppShell {...shell}>
      <div className="toolbar">
        <h1 className="page-title">{t("noModulesTitle")}</h1>
      </div>
      <Card>
        <p>{t("noModulesBody")}</p>
      </Card>
    </AppShell>
  );
}
