import { useState, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { Pill } from "@/shared/components/Pill";
import { DataTable } from "@/shared/components/DataTable";
import { TextField, SelectField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { deactivateTenant, getTenant, getTenantOwnerAccounts, reactivateTenant, updateTenant } from "@/shared/api/tenants";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { accountStatusLabels, translateEnum } from "@/shared/i18n/translations";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";

export function TenantDetailPage() {
  const { token } = useAuth();
  const { id } = useParams<{ id: string }>();
  const tenantId = Number(id);
  const navigate = useNavigate();
  const { language, t } = useLanguage();
  const shell = usePlatformAdminShell();
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useApiData(() => getTenant(token!, tenantId), [token, id, refreshKey]);
  const ownerAccountsState = useApiData(() => getTenantOwnerAccounts(token!, tenantId), [token, id, refreshKey]);

  const [form, setForm] = useState<{ name: string; timezone: string; phoneLine: string } | null>(null);
  const tenant = state.status === "success" ? state.data : null;
  if (tenant && !form) {
    setForm({ name: tenant.name, timezone: tenant.timezone, phoneLine: tenant.phoneLine ?? "" });
  }

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    if (!form) return;
    await updateTenant(token!, tenantId, { name: form.name, timezone: form.timezone, phoneLine: form.phoneLine || null });
    setRefreshKey((k) => k + 1);
  }

  async function handleToggleStatus() {
    if (!tenant) return;
    if (tenant.status === "Active") {
      await deactivateTenant(token!, tenantId);
    } else {
      await reactivateTenant(token!, tenantId);
    }
    setRefreshKey((k) => k + 1);
  }

  return (
    <AppShell {...shell}>
      <div className="breadcrumb">{t("tenants")} / {tenant?.name ?? "…"}</div>
      {tenant && form && (
        <>
          <h1 className="page-title" style={{ display: "flex", alignItems: "center", gap: "var(--space-3)", marginBottom: "var(--space-4)" }}>
            {tenant.name}{" "}
            <Pill variant={tenant.status === "Active" ? "success" : "neutral"}>
              {tenant.status === "Active" ? t("statusActive") : t("statusInactive")}
            </Pill>
          </h1>
          <Card>
            <form onSubmit={handleSave}>
              <TextField label={t("businessName")} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              <SelectField
                label={t("timezone")}
                value={form.timezone}
                onChange={(e) => setForm({ ...form, timezone: e.target.value })}
                options={[{ value: "Asia/Baku", label: "Asia/Baku" }]}
              />
              <TextField label={t("phoneLine")} value={form.phoneLine} onChange={(e) => setForm({ ...form, phoneLine: e.target.value })} />
              <div className="actions">
                <Button type="submit">{t("saveChanges")}</Button>
                <Button type="button" variant="danger" onClick={handleToggleStatus}>
                  {tenant.status === "Active" ? t("deactivateTenant") : t("reactivateTenant")}
                </Button>
              </div>
            </form>
          </Card>
          <Card>
            <h2 style={{ marginBottom: "var(--space-3)" }}>{t("ownerAccounts")}</h2>
            {ownerAccountsState.status === "error" && <div className="field-error">{t("failedToLoadOwnerAccounts")}</div>}
            <DataTable
              loading={ownerAccountsState.status === "loading"}
              rows={ownerAccountsState.status === "success" ? ownerAccountsState.data : []}
              rowKey={(a) => a.id}
              emptyMessage={t("noOwnerAccountsYet")}
              columns={[
                { header: t("colName"), render: (a) => a.name },
                { header: t("colEmail"), render: (a) => a.email },
                {
                  header: t("colStatus"),
                  render: (a) => (
                    <Pill variant={a.status === "Active" ? "success" : "warning"}>
                      {translateEnum(accountStatusLabels, a.status, language)}
                    </Pill>
                  ),
                },
              ]}
            />
          </Card>
          <div className="actions">
            <Button variant="secondary" onClick={() => navigate("/admin/tenants")}>
              {t("backToList")}
            </Button>
          </div>
        </>
      )}
    </AppShell>
  );
}
