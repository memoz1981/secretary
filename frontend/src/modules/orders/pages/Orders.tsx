import { useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Button } from "@/shared/components/Button";
import { Card } from "@/shared/components/Card";
import { DataTable } from "@/shared/components/DataTable";
import { TextField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { getOrders, getOrderSettings, updateOrderSettings } from "@/modules/orders/api/orders";
import type { OrderResponse } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function OrdersPage() {
  const { token, role } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useApiData(() => getOrders(token!), [token, refreshKey]);

  const orders = state.status === "success" ? state.data : [];

  return (
    <AppShell {...shell}>
      <h1 className="page-title">{t("orders")}</h1>
      <div className="subtitle">{t("ordersSubtitle")}</div>

      <DeliveryPromise canEdit={role === "Owner"} onSaved={() => setRefreshKey((k) => k + 1)} />

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

/** The one editable thing on this page. Orders themselves arrive by phone — there is no reason
 *  to type one in, and a form that let you would be a form that disagreed with the recording. */
function DeliveryPromise({ canEdit, onSaved }: { canEdit: boolean; onSaved: () => void }) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [refreshKey, setRefreshKey] = useState(0);
  const settings = useApiData(() => getOrderSettings(token!), [token, refreshKey]);
  const [draft, setDraft] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  if (settings.status !== "success") {
    return null;
  }

  const value = draft ?? String(settings.data.leadWorkingDays);

  return (
    <Card>
      <h2>{t("deliveryPromise")}</h2>
      <p className="sub">{t("deliveryPromiseHelp")}</p>
      <div className="toolbar" style={{ alignItems: "flex-end", gap: "var(--space-3)" }}>
        <TextField
          label={t("leadWorkingDays")}
          value={value}
          disabled={!canEdit}
          onChange={(e) => setDraft(e.target.value)}
        />
        {canEdit && (
          <Button
            loading={saving}
            onClick={async () => {
              const days = Number(value);
              if (Number.isNaN(days) || days < 0 || days > 14) return;
              setSaving(true);
              try {
                await updateOrderSettings(token!, days);
                setDraft(null);
                setRefreshKey((k) => k + 1);
                onSaved();
              } finally {
                setSaving(false);
              }
            }}
          >
            {t("save")}
          </Button>
        )}
      </div>
    </Card>
  );
}
