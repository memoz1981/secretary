import { useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Pill } from "@/shared/components/Pill";
import { DataTable } from "@/shared/components/DataTable";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { getOrders, setOrderStatus } from "@/modules/orders/api/orders";
import type { OrderResponse } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { orderStatusLabels, translateEnum } from "@/shared/i18n/translations";
import { formatDayMonth } from "@/shared/lib/dates";

export function OrdersPage() {
  const { token, role } = useAuth();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();
  const [refreshKey, setRefreshKey] = useState(0);
  const [busyId, setBusyId] = useState<number | null>(null);
  const state = useApiData(() => getOrders(token!), [token, refreshKey]);

  const canEdit = role === "Owner";
  const orders = state.status === "success" ? state.data : [];

  async function setStatus(id: number, status: "Delivered" | "Cancelled") {
    setBusyId(id);
    try {
      await setOrderStatus(token!, id, status);
      setRefreshKey((k) => k + 1);
    } finally {
      setBusyId(null);
    }
  }

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
            // Product and quantity as separate columns: an order is usually one or two lines,
            // and "Sirab × 10" in a single cell cannot be scanned down a page.
            header: t("product"),
            render: (o: OrderResponse) => (
              <span style={{ display: "flex", flexDirection: "column" }}>
                {o.lines.map((l, i) => (
                  <span key={i}>{l.productName}</span>
                ))}
              </span>
            ),
          },
          {
            header: t("colQuantity"),
            className: "mono",
            render: (o: OrderResponse) => (
              <span style={{ display: "flex", flexDirection: "column" }}>
                {o.lines.map((l, i) => (
                  <span key={i}>
                    {l.quantity} {l.unit}
                  </span>
                ))}
              </span>
            ),
          },
          { header: t("colTotal"), render: (o: OrderResponse) => `${o.total} AZN`, className: "mono" },
          { header: t("colDeliveryDay"), render: (o: OrderResponse) => (o.requestedDeliveryDate ? formatDayMonth(o.requestedDeliveryDate, language) : "—") },
          { header: t("colAddress"), render: (o: OrderResponse) => o.deliveryAddress },
          {
            header: t("colStatus"),
            render: (o: OrderResponse) => (
              <Pill variant={o.status === "Delivered" ? "success" : o.status === "Cancelled" ? "critical" : "neutral"}>
                {translateEnum(orderStatusLabels, o.status, language)}
              </Pill>
            ),
          },
          ...(canEdit
            ? [
                {
                  // Whether an order arrived is a fact only a person has — the call cannot know
                  // it, so this is the one thing staff do to an order after it is taken.
                  header: "",
                  render: (o: OrderResponse) =>
                    o.status !== "Placed" ? null : (
                      <span className="row-actions">
                        <button
                          className="link"
                          disabled={busyId === o.id}
                          onClick={() => void setStatus(o.id, "Delivered")}
                        >
                          {t("markDelivered")}
                        </button>
                        <button
                          className="link danger"
                          disabled={busyId === o.id}
                          onClick={() => void setStatus(o.id, "Cancelled")}
                        >
                          {t("markCancelled")}
                        </button>
                      </span>
                    ),
                },
              ]
            : []),
        ]}
      />
    </AppShell>
  );
}
