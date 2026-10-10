import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { DataTable } from "@/shared/components/DataTable";
import { Pill } from "@/shared/components/Pill";
import { Button } from "@/shared/components/Button";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { getTenants } from "@/shared/api/tenants";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { businessTypeLabels, moduleLabels, translateEnum } from "@/shared/i18n/translations";
import { MODULE_PRESENTATION } from "@/modules/presentation";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";
import { BUSINESS_TYPES, type BusinessType, type Module } from "@/shared/api/types";
import { MODULE_REGISTRY } from "@/modules/registry";

export function TenantListPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const { language, t } = useLanguage();
  const shell = usePlatformAdminShell();
  const [search, setSearch] = useState("");

  // Filtering happens here rather than on the server. The search box is already a round trip;
  // these three narrow what came back, and the list is small enough that a request per checkbox
  // would be slower than the filtering it replaces.
  const [status, setStatus] = useState<"" | "Active" | "Inactive">("");
  const [businessType, setBusinessType] = useState<BusinessType | "">("");
  const [modules, setModules] = useState<Module[]>([]);

  const state = useApiData(() => getTenants(token!, search || undefined), [token, search]);

  const all = state.status === "success" ? state.data : [];
  const rows = all.filter((tenant) => {
    if (status !== "" && tenant.status !== status) return false;
    if (businessType !== "" && tenant.businessType !== businessType) return false;
    // Every module ticked, not any: ticking two asks for tenants holding both, which is the
    // question worth asking of a grant list. Any-of would just be a longer way to say "some".
    return modules.every((module) => tenant.enabledModules.includes(module));
  });

  const filtered = status !== "" || businessType !== "" || modules.length > 0;

  function toggleModule(module: Module) {
    setModules((current) =>
      current.includes(module) ? current.filter((m) => m !== module) : [...current, module],
    );
  }

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("tenants")}
      </h1>
      <div className="toolbar">
        <div className="filter-bar">
          <input
            type="text"
            placeholder={t("searchTenants")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ minWidth: 240 }}
          />
          <select value={status} onChange={(e) => setStatus(e.target.value as typeof status)}>
            <option value="">{t("allStatuses")}</option>
            <option value="Active">{t("statusActive")}</option>
            <option value="Inactive">{t("statusInactive")}</option>
          </select>
          <select
            value={businessType}
            onChange={(e) => setBusinessType(e.target.value as BusinessType | "")}
          >
            <option value="">{t("allBusinessTypes")}</option>
            {BUSINESS_TYPES.map((type) => (
              <option key={type} value={type}>
                {translateEnum(businessTypeLabels, type, language)}
              </option>
            ))}
          </select>
        </div>
        <Button onClick={() => navigate("/admin/tenants/new")}>{t("createTenant")}</Button>
      </div>

      {/* Checkboxes rather than a multi-select control: there are four of them, and a listbox
          you have to ctrl-click hides which ones are on behind a scroll. */}
      <div className="filter-bar" style={{ marginBottom: "var(--space-3)" }}>
        <span className="sub">{t("filterByModules")}</span>
        {MODULE_REGISTRY.map((manifest) => (
          <label key={manifest.key} className="checkbox-row">
            <input
              type="checkbox"
              checked={modules.includes(manifest.key)}
              onChange={() => toggleModule(manifest.key)}
            />
            <span>{translateEnum(moduleLabels, manifest.key, language)}</span>
          </label>
        ))}
        {/* Pushed to the right-hand end, above the table it describes. Small and italic so it
            reads as a note about the result rather than as another filter to set. */}
        {filtered && (
          <span className="filter-result">
            <span className="filter-count">
              {t("matchingTenants").replace("{n}", String(rows.length))}
            </span>
            <button
              className="link"
              onClick={() => {
                setStatus("");
                setBusinessType("");
                setModules([]);
              }}
            >
              {t("clearFilters")}
            </button>
          </span>
        )}
      </div>
      {state.status === "error" && <div className="field-error">{t("failedToLoadTenants")}</div>}
      <DataTable
        loading={state.status === "loading"}
        rows={rows}
        rowKey={(tenant) => tenant.id}
        onRowClick={(tenant) => navigate(`/admin/tenants/${tenant.id}`)}
        emptyMessage={t("noTenantsYet")}
        columns={[
          { header: t("colName"), render: (tenant) => tenant.name },
          {
            header: t("businessType"),
            render: (tenant) =>
              tenant.businessType === null ? (
                <span className="sub">{t("businessTypeNotSet")}</span>
              ) : (
                translateEnum(businessTypeLabels, tenant.businessType, language)
              ),
          },
          {
            // What the admin is really managing. The list showed everything except this, so
            // finding out what a client had bought meant opening them one at a time.
            header: t("tenantModules"),
            render: (tenant) =>
              tenant.enabledModules.length === 0 ? (
                <span className="sub">{t("noModules")}</span>
              ) : (
                <span style={{ display: "inline-flex", flexWrap: "wrap", gap: "var(--space-1)" }}>
                  {tenant.enabledModules.map((module) => (
                    <Pill key={module} tone={MODULE_PRESENTATION[module].tone}>
                      {translateEnum(moduleLabels, module, language)}
                    </Pill>
                  ))}
                </span>
              ),
          },
          { header: t("colPhoneLine"), render: (tenant) => tenant.phoneLine ?? t("unassigned") },
          {
            header: t("colStatus"),
            render: (tenant) => (
              <Pill variant={tenant.status === "Active" ? "success" : "neutral"}>
                {tenant.status === "Active" ? t("statusActive") : t("statusInactive")}
              </Pill>
            ),
          },
          { header: t("colCreated"), render: (tenant) => new Date(tenant.createdAt).toLocaleDateString(), className: "mono" },
          {
            // A second destination from the same row: the row itself opens the settings, this
            // opens what the account is actually doing.
            header: "",
            render: (tenant) => (
              // Not secondary. This is the second thing anybody comes to this list to do, and
              // in a grey outline on a dark theme it was nearly invisible.
              <Button
                size="sm"
                onClick={(e) => {
                  e.stopPropagation();
                  navigate(`/admin/tenants/${tenant.id}/dashboard`);
                }}
              >
                {t("tenantDashboard")}
              </Button>
            ),
          },
        ]}
      />
      <div className="note">{t("rowClickToTenantDetail")}</div>
    </AppShell>
  );
}
