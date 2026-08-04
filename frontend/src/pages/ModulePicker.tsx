import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import type { ModuleManifest } from "@/modules/registry";

/** Shown only to a tenant holding more than one module, which by design is the rare case.
 *
 * Styled as a card grid like the public landing page on purpose: a tenant meeting this screen
 * has already seen that layout, and the tiles mean the same thing in both places. */
export function ModulePicker({ modules }: { modules: ModuleManifest[] }) {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const shell = useBusinessShell();

  return (
    <AppShell {...shell}>
      <div className="toolbar">
        <h1 className="page-title">{t("pickerTitle")}</h1>
      </div>

      <div className="stat-grid" style={{ marginTop: "var(--space-4)" }}>
        {modules.map((module) => (
          // The click target wraps the Card rather than living on it: Card is a plain
          // presentational box used on every page, and giving it behaviour for one screen's
          // sake would put an unused handler on all of them.
          <div
            key={module.key}
            role="button"
            tabIndex={0}
            style={{ cursor: "pointer" }}
            onClick={() => navigate(module.home)}
            onKeyDown={(event) => {
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                navigate(module.home);
              }
            }}
          >
            <Card>
              <h2>{t(module.titleKey)}</h2>
              <p className="sub" style={{ marginTop: "var(--space-2)" }}>{t(module.descriptionKey)}</p>
            </Card>
          </div>
        ))}
      </div>
    </AppShell>
  );
}
