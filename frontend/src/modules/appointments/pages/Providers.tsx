import { useState, type FormEvent } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { DataTable } from "@/shared/components/DataTable";
import { TextField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import {
  createProvider,
  getProviderServiceMatrix,
  removeProvider,
  setProviderServiceAssignment,
  updateProvider,
} from "@/modules/appointments/api/providers";
import type { ProviderResponse } from "@/shared/api/types";
import { isRequired } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function ProvidersPage() {
  const { token, role } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const [refreshKey, setRefreshKey] = useState(0);
  const matrixState = useApiData(() => getProviderServiceMatrix(token!), [token, refreshKey]);
  const [editing, setEditing] = useState<ProviderResponse | "new" | null>(null);

  const canEdit = role === "Owner";
  const matrix = matrixState.status === "success" ? matrixState.data : null;
  const providers = matrix?.providers ?? [];
  const offerings = matrix?.serviceOfferings ?? [];

  // One Set of "providerId:serviceOfferingId" keys for the checked cells — checkbox state
  // reads in O(1) and flips optimistically while the PUT is in flight.
  const [optimistic, setOptimistic] = useState<Record<string, boolean>>({});
  const activeCells = new Set(
    (matrix?.assignments ?? []).filter((a) => a.active).map((a) => `${a.providerId}:${a.serviceOfferingId}`),
  );

  function cellChecked(providerId: number, serviceOfferingId: number): boolean {
    const key = `${providerId}:${serviceOfferingId}`;
    return optimistic[key] ?? activeCells.has(key);
  }

  async function toggleCell(providerId: number, serviceOfferingId: number) {
    const key = `${providerId}:${serviceOfferingId}`;
    const next = !cellChecked(providerId, serviceOfferingId);
    setOptimistic((o) => ({ ...o, [key]: next }));
    try {
      await setProviderServiceAssignment(token!, providerId, { serviceOfferingId, active: next });
    } catch {
      setOptimistic((o) => ({ ...o, [key]: !next }));
    }
  }

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-5)" }}>
        {t("providers")}
      </h1>

      <Card>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-3)" }}>
          <h2>{t("providers")}</h2>
          {canEdit && (
            <Button size="sm" onClick={() => setEditing("new")}>
              {t("addProvider")}
            </Button>
          )}
        </div>
        {matrixState.status === "error" && <div className="field-error">{t("failedToLoadProviders")}</div>}
        <DataTable
          loading={matrixState.status === "loading"}
          rows={providers}
          rowKey={(p) => p.id}
          emptyMessage={t("noProvidersYet")}
          columns={[
            { header: t("colName"), render: (p) => p.name },
            ...(canEdit
              ? [
                  {
                    header: "",
                    render: (p: ProviderResponse) => (
                      <span className="row-actions">
                        <button className="link" onClick={() => setEditing(p)}>
                          {t("edit")}
                        </button>
                        <button
                          className="link danger"
                          onClick={async () => {
                            await removeProvider(token!, p.id);
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
      </Card>

      {providers.length > 0 && offerings.length > 0 && (
        <Card>
          <h2 style={{ marginBottom: "var(--space-2)" }}>{t("serviceMatrix")}</h2>
          <div className="note" style={{ marginBottom: "var(--space-3)" }}>
            {t("serviceMatrixHint")}
          </div>
          <div className="table-scroll">
            <table className="data">
              <thead>
                <tr>
                  <th>{t("provider")}</th>
                  {offerings.map((s) => (
                    <th key={s.id} style={{ textAlign: "center" }}>
                      {s.name}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {providers.map((p) => (
                  <tr key={p.id}>
                    <td>{p.name}</td>
                    {offerings.map((s) => (
                      <td key={s.id} style={{ textAlign: "center" }}>
                        <input
                          type="checkbox"
                          checked={cellChecked(p.id, s.id)}
                          onChange={() => toggleCell(p.id, s.id)}
                        />
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {editing && (
        <ProviderEditor
          existing={editing === "new" ? undefined : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            setOptimistic({});
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
    </AppShell>
  );
}

function ProviderEditor({
  existing,
  onClose,
  onSaved,
}: {
  existing?: ProviderResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [name, setName] = useState(existing?.name ?? "");
  const [error, setError] = useState<string | undefined>();
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!isRequired(name)) {
      setError(t("nameRequired"));
      return;
    }
    setSubmitting(true);
    try {
      if (existing) {
        await updateProvider(token!, existing.id, { name });
      } else {
        await createProvider(token!, { name });
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
          <h1>{existing ? t("editProvider") : t("addProvider")}</h1>
          <button className="close-btn" onClick={onClose}>
            ✕
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <TextField label={t("name")} value={name} onChange={(e) => setName(e.target.value)} error={error} />
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
