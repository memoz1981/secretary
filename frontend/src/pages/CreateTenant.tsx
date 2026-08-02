import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { TextField, SelectField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { createTenant } from "@/shared/api/tenants";
import { isRequired, isValidEmail, isMinLength } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";

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
