import { AppShell } from "@/shared/components/AppShell";
import { DataTable } from "@/shared/components/DataTable";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { getOrderCustomers } from "@/modules/orders/api/orders";
import type { OrderCustomerResponse } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

/** Order customers — not the appointment module's clients. Different people, different schema,
 *  deliberately no way to see one from the other.
 *
 *  Read-only: every row here was created by a phone call, and the id column is the customer
 *  number the caller was read at the end of theirs. */
export function OrderCustomersPage() {
  const { token } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const state = useApiData(() => getOrderCustomers(token!), [token]);

  const customers = state.status === "success" ? state.data : [];

  return (
    <AppShell {...shell}>
      <h1 className="page-title">{t("navOrderCustomers")}</h1>
      <div className="subtitle">{t("orderCustomersSubtitle")}</div>
      {state.status === "error" && <div className="field-error">{t("failedToLoadCustomers")}</div>}
      <DataTable
        loading={state.status === "loading"}
        rows={customers}
        rowKey={(c) => c.id}
        emptyMessage={t("noCustomersYet")}
        columns={[
          { header: t("customerNumber"), render: (c) => String(c.id), className: "mono" },
          { header: t("name"), render: (c: OrderCustomerResponse) => c.name ?? "—" },
          {
            header: t("colPhone"),
            render: (c: OrderCustomerResponse) => c.phoneNumbers.join(", ") || "—",
            className: "mono",
          },
          {
            header: t("colAddress"),
            render: (c: OrderCustomerResponse) => c.addresses.join(" / ") || "—",
          },
        ]}
      />
    </AppShell>
  );
}
