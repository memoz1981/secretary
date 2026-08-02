import { useState, type FormEvent } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Button } from "@/shared/components/Button";
import { DataTable } from "@/shared/components/DataTable";
import { TextField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { createServiceOffering, getServiceOfferings, removeServiceOffering, updateServiceOffering } from "@/modules/appointments/api/serviceOfferings";
import type { ServiceOfferingResponse } from "@/shared/api/types";
import { isPositiveNumber, isRequired } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function ServicesPage() {
  const { token, role } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useApiData(() => getServiceOfferings(token!), [token, refreshKey]);
  const [editing, setEditing] = useState<ServiceOfferingResponse | "new" | null>(null);

  const canEdit = role === "Owner";
  const services = state.status === "success" ? state.data : [];

  return (
    <AppShell {...shell}>
      <h1 className="page-title">{t("services")}</h1>
      <div className="subtitle">{canEdit ? t("ownerViewEditable") : t("staffViewReadOnly")}</div>
      {canEdit && (
        <div className="toolbar" style={{ justifyContent: "flex-end" }}>
          <Button onClick={() => setEditing("new")}>{t("addService")}</Button>
        </div>
      )}
      {state.status === "error" && <div className="field-error">{t("failedToLoadServices")}</div>}
      <DataTable
        loading={state.status === "loading"}
        rows={services}
        rowKey={(s) => s.id}
        emptyMessage={t("noServicesYet")}
        columns={[
          { header: t("service"), render: (s) => s.name },
          { header: t("colPrice"), render: (s) => `${s.price} AZN`, className: "mono" },
          { header: t("colDuration"), render: (s) => `${s.durationMinutes} min` },
          ...(canEdit
            ? [
                {
                  header: "",
                  render: (s: ServiceOfferingResponse) => (
                    <span className="row-actions">
                      <button className="link" onClick={() => setEditing(s)}>
                        {t("edit")}
                      </button>
                      <button
                        className="link danger"
                        onClick={async () => {
                          await removeServiceOffering(token!, s.id);
                          setRefreshKey((k) => k + 1);
                        }}
                      >
                        {t("remove")}
                      </button>
                    </span>
                  ),
                },
              ]
            : []),
        ]}
      />
      {editing && (
        <ServiceEditor
          existing={editing === "new" ? undefined : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
    </AppShell>
  );
}

function ServiceEditor({
  existing,
  onClose,
  onSaved,
}: {
  existing?: ServiceOfferingResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [name, setName] = useState(existing?.name ?? "");
  const [price, setPrice] = useState(existing ? String(existing.price) : "");
  const [duration, setDuration] = useState(existing ? String(existing.durationMinutes) : "");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const nextErrors: Record<string, string> = {};
    if (!isRequired(name)) nextErrors.name = t("serviceNameRequired");
    if (!isPositiveNumber(price)) nextErrors.price = t("enterValidPrice");
    if (!isPositiveNumber(duration)) nextErrors.duration = t("enterValidDuration");
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    setSubmitting(true);
    try {
      const request = { name, price: Number(price), durationMinutes: Number(duration) };
      if (existing) {
        await updateServiceOffering(token!, existing.id, request);
      } else {
        await createServiceOffering(token!, request);
      }
      onSaved();
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <div className="backdrop" onClick={onClose} />
      <div className="panel">
        <div className="panel-header">
          <h1>{existing ? t("editService") : t("addService")}</h1>
          <button className="close-btn" onClick={onClose}>
            ✕
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <TextField label={t("name")} value={name} onChange={(e) => setName(e.target.value)} error={errors.name} />
          <TextField label={t("priceAzn")} value={price} onChange={(e) => setPrice(e.target.value)} error={errors.price} />
          <TextField label={t("durationMin")} value={duration} onChange={(e) => setDuration(e.target.value)} error={errors.duration} />
          <div className="actions">
            <Button type="submit" loading={submitting}>
              {t("save")}
            </Button>
            <Button type="button" variant="secondary" onClick={onClose}>
              {t("cancel")}
            </Button>
          </div>
        </form>
      </div>
    </>
  );
}
