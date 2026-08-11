import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { TextField, SelectField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { createTenant, setTenantModule } from "@/shared/api/tenants";
import { MODULES, type Module } from "@/shared/api/types";
import { isRequired, isValidEmail, isMinLength } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";
import { ModuleToggle } from "@/shared/components/ModuleToggle";

export function CreateTenantPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const { t } = useLanguage();
  const shell = usePlatformAdminShell();
  const [name, setName] = useState("");
  const [timezone, setTimezone] = useState("Asia/Baku");
  const [phoneLine, setPhoneLine] = useState("");
  const [ownerName, setOwnerName] = useState("");
  const [ownerEmail, setOwnerEmail] = useState("");
  const [ownerPassword, setOwnerPassword] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  // Nothing ticked. Choosing what a client gets is the point of the screen, and a pre-tick is
  // a decision made on the admin's behalf that they then have to notice to undo.
  const [modules, setModules] = useState<Record<string, boolean>>({});

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const nextErrors: Record<string, string> = {};
    if (!isRequired(name)) nextErrors.name = t("businessNameRequired");
    if (!isRequired(ownerName)) nextErrors.ownerName = t("ownerNameRequired");
    if (!isValidEmail(ownerEmail)) nextErrors.ownerEmail = t("enterValidEmail");
    if (!isMinLength(ownerPassword, 8)) nextErrors.ownerPassword = t("atLeast8Characters");
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    setSubmitting(true);
    try {
      const result = await createTenant(token!, {
        name,
        timezone,
        phoneLine: phoneLine || null,
        ownerName,
        ownerEmail,
        ownerPassword,
      });
      // Creation grants Appointment and nothing else, so only the differences are sent. Done
      // after the tenant exists because a module is granted TO something.
      for (const module of MODULES) {
        const wanted = modules[module] ?? false;
        const granted = module === "Appointment";
        if (wanted !== granted) {
          await setTenantModule(token!, result.tenant.id, module, wanted);
        }
      }

      // Back to the list, where the new tenant appears with the modules just granted. The
      // detail page was a dead end: the admin had finished, and the next thing they want is to
      // see it sitting alongside the others.
      navigate("/admin/tenants");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AppShell {...shell}>
      <div className="breadcrumb">{t("breadcrumbTenantsCreate")}</div>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("createTenant")}
      </h1>
      <Card>
        <form onSubmit={handleSubmit} style={{ maxWidth: 480 }}>
          <TextField
            label={t("businessName")}
            placeholder={t("businessNamePlaceholder")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            error={errors.name}
          />
          <SelectField
            label={t("timezone")}
            value={timezone}
            onChange={(e) => setTimezone(e.target.value)}
            options={[{ value: "Asia/Baku", label: "Asia/Baku" }]}
          />
          <TextField
            label={t("phoneLine")}
            placeholder={t("phoneLinePlaceholder")}
            value={phoneLine}
            onChange={(e) => setPhoneLine(e.target.value)}
          />
          <div className="section-label">{t("firstOwnerAccount")}</div>
          <TextField
            label={t("ownerName")}
            placeholder={t("fullName")}
            value={ownerName}
            onChange={(e) => setOwnerName(e.target.value)}
            error={errors.ownerName}
          />
          {/* The browser reads these as its own sign-in fields and fills them with the admin's
              saved credentials, which is how a tenant nearly got created under the admin's
              email. "new-password" is the one value browsers honour for "this is not a login". */}
          <TextField
            label={t("ownerEmail")}
            placeholder="owner@business.com"
            value={ownerEmail}
            onChange={(e) => setOwnerEmail(e.target.value)}
            error={errors.ownerEmail}
            autoComplete="off"
          />
          <TextField
            label={t("ownerPassword")}
            type="password"
            value={ownerPassword}
            onChange={(e) => setOwnerPassword(e.target.value)}
            error={errors.ownerPassword}
            autoComplete="new-password"
          />
          <div className="section-label">{t("tenantModules")}</div>
          {MODULES.map((module: Module) => (
            <ModuleToggle
              key={module}
              module={module}
              checked={modules[module] ?? false}
              disabled={submitting}
              // Functional update, not a spread of the captured `modules`. Two toggles clicked
              // before React re-renders both read the same stale object, and the second write
              // silently undoes the first — which is the selections appearing to flip.
              onChange={(next) => setModules((current) => ({ ...current, [module]: next }))}
            />
          ))}
          <div className="actions">
            <Button type="submit" loading={submitting}>
              {t("createTenant")}
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate("/admin/tenants")}>
              {t("cancel")}
            </Button>
          </div>
        </form>
      </Card>
    </AppShell>
  );
}
