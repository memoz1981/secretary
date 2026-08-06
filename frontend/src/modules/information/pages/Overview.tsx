import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useBusinessShell } from "@/shared/lib/appShellProps";

/** A real page in a real module, with nothing behind it yet.
 *
 * It exists so the multi-module paths can be exercised before a second module is actually
 * built: the picker only renders for a tenant holding two, and the module switcher in the
 * sidebar appears on the same condition. Without something to be the second one, both were
 * written and never seen.
 *
 * Says plainly that it is a placeholder rather than showing an empty table that reads as
 * broken. */
export function InformationOverviewPage() {
  const { t } = useLanguage();
  const shell = useBusinessShell();

  return (
    <AppShell {...shell}>
      <div className="toolbar">
        <h1 className="page-title">{t("moduleInfoTitle")}</h1>
      </div>
      <Card>
        <h2>{t("modulePlaceholderTitle")}</h2>
        <p className="sub" style={{ marginTop: "var(--space-2)" }}>{t("modulePlaceholderBody")}</p>
      </Card>
    </AppShell>
  );
}
