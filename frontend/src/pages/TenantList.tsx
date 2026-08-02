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
import { usePlatformAdminShell } from "@/shared/lib/appShellProps";

export function TenantListPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const { t } = useLanguage();
  const shell = usePlatformAdminShell();
  const [search, setSearch] = useState("");
  const state = useApiData(() => getTenants(token!, search || undefined), [token, search]);

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
        </div>
        <Button onClick={() => navigate("/admin/tenants/new")}>{t("createTenant")}</Button>
      </div>
      {state.status === "error" && <div className="field-error">{t("failedToLoadTenants")}</div>}
      <DataTable
        loading={state.status === "loading"}
        rows={state.status === "success" ? state.data : []}
        rowKey={(tenant) => tenant.id}
        onRowClick={(tenant) => navigate(`/admin/tenants/${tenant.id}`)}
        emptyMessage={t("noTenantsYet")}
        columns={[
          { header: t("colName"), render: (tenant) => tenant.name },
          { header: t("colTimezone"), render: (tenant) => tenant.timezone },
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
        ]}
      />
      <div className="note">{t("rowClickToTenantDetail")}</div>
    </AppShell>
  );
}
