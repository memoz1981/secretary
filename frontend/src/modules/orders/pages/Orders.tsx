import { AppShell } from "@/shared/components/AppShell";
import { DataTable } from "@/shared/components/DataTable";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { getOrders } from "@/modules/orders/api/orders";
import type { OrderResponse } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function OrdersPage() {
  const { token } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const state = useApiData(() => getOrders(token!), [token]);

  const orders = state.status === "success" ? state.data : [];

  return (
    <AppShell {...shell}>
      <h1 className="page-title">{t("orders")}</h1>
      <div className="subtitle">{t("ordersSubtitle")}</div>

      {state.status === "error" && <div className="field-error">{t("failedToLoadOrders")}</div>}
      <DataTable
        loading={state.status === "loading"}
        rows={orders}
        rowKey={(o) => o.id}
        emptyMessage={t("noOrdersYet")}
        columns={[
          { header: "#", render: (o) => String(o.id), className: "mono" },
          {
            header: t("customer"),
            render: (o: OrderResponse) => o.customerName ?? `#${o.customerId}`,
          },
          {
            header: t("colItems"),
            render: (o: OrderResponse) =>
              o.lines.map((l) => `${l.productName} × ${l.quantity}`).join(", "),
          },
          { header: t("colTotal"), render: (o: OrderResponse) => `${o.total} AZN`, className: "mono" },
          { header: t("colDeliveryDay"), render: (o: OrderResponse) => o.requestedDeliveryDate ?? "—" },
          { header: t("colAddress"), render: (o: OrderResponse) => o.deliveryAddress },
          { header: t("colStatus"), render: (o: OrderResponse) => o.status },
        ]}
      />
    </AppShell>
  );
}
