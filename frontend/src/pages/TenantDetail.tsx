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
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { accountStatusLabels, translateEnum } from "@/shared/i18n/translations";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";
import { ModuleToggle } from "@/shared/components/ModuleToggle";

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

  // Staged, not live. Toggling used to save on the spot, which meant idly clicking through the
  // switches to see what was there silently changed what a paying client could use. Nothing
  // leaves the page until Save.
  const [moduleDraft, setModuleDraft] = useState<Record<string, boolean> | null>(null);
  const [savingModules, setSavingModules] = useState(false);

  /// Saving modules changes nothing visible — the toggles already show what you chose — so
  /// without a word confirming it, the only way to know it worked was to leave and come back.
  const [justSaved, setJustSaved] = useState(false);

  const serverModules = modulesState.status === "success" ? modulesState.data : null;
  if (serverModules && !moduleDraft) {
    setModuleDraft(Object.fromEntries(serverModules.map((m) => [m.module, m.enabled])));
  }

  const moduleChanges = serverModules && moduleDraft
    ? serverModules.filter((m) => moduleDraft[m.module] !== m.enabled)
    : [];

  const [form, setForm] =
    useState<{ name: string; timezone: string; phoneLine: string; showCallCosts: boolean } | null>(null);
  const tenant = state.status === "success" ? state.data : null;
  if (tenant && !form) {
    setForm({
      name: tenant.name,
      timezone: tenant.timezone,
      phoneLine: tenant.phoneLine ?? "",
      showCallCosts: tenant.showCallCosts,
    });
  }

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    if (!form) return;
    await updateTenant(token!, tenantId, {
      name: form.name,
      timezone: form.timezone,
      phoneLine: form.phoneLine || null,
      showCallCosts: form.showCallCosts,
    });
    setRefreshKey((k) => k + 1);
  }

  async function handleSaveModules() {
    if (moduleChanges.length === 0) return;
    setSavingModules(true);
    try {
      // One request per change rather than a bulk endpoint: the changes are a handful at most,
      // and each one is independently meaningful in the audit trail.
      for (const changed of moduleChanges) {
        await setTenantModule(token!, tenantId, changed.module, moduleDraft![changed.module]);
      }

      // The draft is kept, not cleared. Clearing it unmounted every toggle until the reload
      // came back, so a successful save looked like the selections vanishing and then flipping
      // to something else — the save worked, but nothing on screen said so. Re-reading still
      // happens; when the server's answer arrives it simply agrees, and the Save button
      // disables itself because there is nothing left to change.
      setRefreshKey((k) => k + 1);
      setJustSaved(true);
      window.setTimeout(() => setJustSaved(false), 3000);
    } finally {
      setSavingModules(false);
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
            {/* Beside the name rather than buried below the module switches: what an account is
                doing is the first thing anybody opening it wants, and configuring it is the
                second. */}
            <button
              className="link"
              style={{ marginLeft: "auto", fontSize: "var(--text-base)" }}
              onClick={() => navigate(`/admin/tenants/${tenantId}/dashboard`)}
            >
              {t("tenantDashboard")} →
            </button>
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
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={form.showCallCosts}
                  onChange={(e) => setForm({ ...form, showCallCosts: e.target.checked })}
                />
                <span>{t("showCallCosts")}</span>
              </label>
              <div className="note">{t("showCallCostsExplain")}</div>
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
            {serverModules && moduleDraft && (
              <>
                {serverModules.map((row) => (
                  <ModuleToggle
                    key={row.module}
                    module={row.module}
                    checked={moduleDraft[row.module] ?? false}
                    disabled={savingModules}
                    // Functional update, not a spread of the captured draft: two toggles clicked
                    // before a re-render both read the same stale object and the second undoes
                    // the first, which is the selections appearing to flip.
                    onChange={(next) => setModuleDraft((current) => ({ ...current, [row.module]: next }))}
                  />
                ))}
                <div className="actions" style={{ marginTop: "var(--space-3)" }}>
                  <Button type="button" onClick={handleSaveModules} disabled={moduleChanges.length === 0 || savingModules}>
                    {t("saveChanges")}
                  </Button>
                  {moduleChanges.length > 0 && (
                    <Button type="button" variant="secondary" onClick={() => setModuleDraft(null)} disabled={savingModules}>
                      {t("cancel")}
                    </Button>
                  )}
                  {justSaved && moduleChanges.length === 0 && (
                    <span className="sub" style={{ alignSelf: "center" }}>{t("modulesSaved")}</span>
                  )}
                </div>
              </>
            )}
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
