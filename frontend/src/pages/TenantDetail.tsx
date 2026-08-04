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
import {
  deactivateTenant,
  getTenant,
  getTenantModules,
  getTenantOwnerAccounts,
  reactivateTenant,
  setTenantModule,
  updateTenant,
} from "@/shared/api/tenants";
import type { Module } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { accountStatusLabels, moduleLabels, translateEnum } from "@/shared/i18n/translations";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";
import { findModule } from "@/modules/registry";

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
  const modulesState = useApiData(() => getTenantModules(token!, tenantId), [token, id, refreshKey]);

  // Which module is mid-flight, so every switch locks while one saves rather than letting a
  // second click race the first.
  const [savingModule, setSavingModule] = useState<Module | null>(null);

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

  async function handleToggleModule(module: Module, enabled: boolean) {
    setSavingModule(module);
    try {
      await setTenantModule(token!, tenantId, module, enabled);
      setRefreshKey((k) => k + 1);
    } finally {
      setSavingModule(null);
    }
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
          <Card>
            <h2 style={{ marginBottom: "var(--space-2)" }}>{t("tenantModules")}</h2>
            <p className="sub" style={{ marginBottom: "var(--space-3)" }}>{t("tenantModulesHint")}</p>
            {modulesState.status === "error" && <div className="field-error">{t("somethingWentWrong")}</div>}
            {modulesState.status === "success" &&
              modulesState.data.map((row) => {
                // Granting a module the front end cannot render yet would put a tenant in a
                // picker with a tile that goes nowhere. The switch stays, because the grant is
                // still a real record, but it is disabled and says why.
                const built = findModule(row.module) !== undefined;
                return (
                  <label
                    key={row.module}
                    style={{ display: "flex", alignItems: "center", gap: "var(--space-3)", padding: "var(--space-2) 0" }}
                  >
                    <input
                      type="checkbox"
                      checked={row.enabled}
                      disabled={!built || savingModule !== null}
                      onChange={(e) => handleToggleModule(row.module, e.target.checked)}
                    />
                    <span>{translateEnum(moduleLabels, row.module, language)}</span>
                    {!built && <Pill variant="neutral">{t("moduleNotBuiltYet")}</Pill>}
                  </label>
                );
              })}
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
