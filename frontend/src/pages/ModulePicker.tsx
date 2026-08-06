import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Pill } from "@/shared/components/Pill";
import { IconChip } from "@/shared/components/LandingArt";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { getMyModules } from "@/shared/api/tenants";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { findModule } from "@/modules/registry";
import { MODULE_PRESENTATION } from "@/modules/presentation";

/** Shown to a tenant holding more than one module.
 *
 * Every module the platform sells, not only the ones this tenant bought — the list comes from
 * the API rather than a hardcoded front-end array, so it cannot fall behind what is actually on
 * offer. What they hold is a tile they can press; the rest are visibly there and visibly not
 * theirs, which is how a customer finds out there is more.
 *
 * Tiles, not paragraphs. The same cards as the public landing page, because someone who has
 * signed in already knows what they bought and does not need it explained again. */
export function ModulePicker() {
  const { token } = useAuth();
  const { t } = useLanguage();
  const navigate = useNavigate();
  const shell = useBusinessShell();
  const state = useApiData(() => getMyModules(token!), [token]);

  return (
    <AppShell {...shell}>
      <div className="toolbar">
        <h1 className="page-title">{t("pickerTitle")}</h1>
      </div>

      {state.status === "error" && <div className="field-error">{t("somethingWentWrong")}</div>}

      <div className="card-grid" style={{ marginTop: "var(--space-4)" }}>
        {state.status === "success" &&
          state.data.map((row) => {
            const look = MODULE_PRESENTATION[row.module];
            const manifest = findModule(row.module);

            // Usable means both: granted to this tenant, and actually built. A grant for
            // something the front end cannot render would otherwise be a tile that goes nowhere.
            const usable = row.enabled && manifest !== undefined;

            const status = !row.enabled
              ? t("moduleNoAccess")
              : manifest
                ? t("landingStatusActive")
                : t("landingStatusSoon");

            const tile = (
              <Card className="landing-card" style={usable ? undefined : { opacity: 0.5 }}>
                <div className="module-head">
                  <IconChip name={look.icon} tone={look.tone} />
                  <Pill variant={usable ? "success" : "neutral"}>{status}</Pill>
                </div>
                <h3>{t(look.titleKey)}</h3>
              </Card>
            );

            if (!usable) {
              return <div key={row.module}>{tile}</div>;
            }

            return (
              <div
                key={row.module}
                role="button"
                tabIndex={0}
                style={{ cursor: "pointer" }}
                onClick={() => navigate(manifest!.home)}
                onKeyDown={(event) => {
                  if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    navigate(manifest!.home);
                  }
                }}
              >
                {tile}
              </div>
            );
          })}
      </div>
    </AppShell>
  );
}
