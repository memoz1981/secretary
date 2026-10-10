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
import { BUSINESS_TYPES, type BusinessType } from "@/shared/api/types";
import { MODULE_PRESENTATION } from "@/modules/presentation";
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";

export function TenantListPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const { language, t } = useLanguage();
  const shell = usePlatformAdminShell();
  const [search, setSearch] = useState("");
  const state = useApiData(() => getTenants(token!, search || undefined), [token, search]);

  // Filtered here rather than on the server: the search box is already a round trip, and the
  // list is small enough that narrowing what came back is cheaper than asking again.
  const [businessType, setBusinessType] = useState<BusinessType | "">("");
  const all = state.status === "success" ? state.data : [];
  const rows = businessType === "" ? all : all.filter((tenant) => tenant.businessType === businessType);

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("tenants")}
      </h1>
      <div className="toolbar">
        <div className="filters" style={{ display: "flex", gap: "var(--space-3)" }}>
          <input
            type="text"
            placeholder={t("searchTenants")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ width: 240 }}
          />
          <select
            value={businessType}
            onChange={(e) => setBusinessType(e.target.value as BusinessType | "")}
            style={{ width: "auto" }}
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
              <Button
                size="sm"
                variant="secondary"
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
