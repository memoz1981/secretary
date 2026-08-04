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
import { moduleLabels, translateEnum } from "@/shared/i18n/translations";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";
import { findModule } from "@/modules/registry";
import { Pill } from "@/shared/components/Pill";

export function CreateTenantPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const { language, t } = useLanguage();
  const shell = usePlatformAdminShell();
  const [name, setName] = useState("");
  const [timezone, setTimezone] = useState("Asia/Baku");
  const [phoneLine, setPhoneLine] = useState("");
  const [ownerName, setOwnerName] = useState("");
  const [ownerEmail, setOwnerEmail] = useState("");
  const [ownerPassword, setOwnerPassword] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  // Appointment pre-ticked because the API grants it on creation regardless — a tenant with no
  // module can log in and reach nothing, and it is the only module built. Shown rather than
  // hidden so the default is visible instead of surprising.
  const [modules, setModules] = useState<Record<string, boolean>>({ Appointment: true });

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

      navigate(`/admin/tenants/${result.tenant.id}`);
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
          <TextField
            label={t("ownerEmail")}
            placeholder="owner@business.com"
            value={ownerEmail}
            onChange={(e) => setOwnerEmail(e.target.value)}
            error={errors.ownerEmail}
          />
          <TextField
            label={t("ownerPassword")}
            type="password"
            value={ownerPassword}
            onChange={(e) => setOwnerPassword(e.target.value)}
            error={errors.ownerPassword}
          />
          <div className="section-label">{t("tenantModules")}</div>
          {MODULES.map((module: Module) => {
            const built = findModule(module) !== undefined;
            return (
              <label
                key={module}
                style={{ display: "flex", alignItems: "center", gap: "var(--space-3)", padding: "var(--space-2) 0" }}
              >
                <input
                  type="checkbox"
                  checked={modules[module] ?? false}
                  disabled={!built || submitting}
                  onChange={(e) => setModules({ ...modules, [module]: e.target.checked })}
                />
                <span>{translateEnum(moduleLabels, module, language)}</span>
                {!built && <Pill variant="neutral">{t("moduleNotBuiltYet")}</Pill>}
              </label>
            );
          })}
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
