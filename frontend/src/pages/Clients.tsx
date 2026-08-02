import { useState, type FormEvent } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { DataTable } from "@/shared/components/DataTable";
import { Pill } from "@/shared/components/Pill";
import { TextField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { blackListClient, createClient, getClients, removeClient, undoBlackListClient, updateClient } from "@/shared/api/clients";
import { ApiError } from "@/shared/api/client";
import type { ClientResponse } from "@/shared/api/types";
import { isRequired } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function ClientsPage() {
  const { token } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useApiData(() => getClients(token!), [token, refreshKey]);
  const [editing, setEditing] = useState<ClientResponse | "new" | null>(null);
  const [blacklisting, setBlacklisting] = useState<ClientResponse | null>(null);

  const clients = state.status === "success" ? state.data : [];

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-4)" }}>
        {t("clientsTitle")}
      </h1>
      <div className="subtitle">{t("blackListNote")}</div>
      <div className="toolbar" style={{ justifyContent: "flex-end" }}>
        <Button onClick={() => setEditing("new")}>{t("addClient")}</Button>
      </div>
      {state.status === "error" && <div className="field-error">{t("failedToLoadClients")}</div>}
      <Card>
        <DataTable
          loading={state.status === "loading"}
          rows={clients}
          rowKey={(c) => c.id}
          emptyMessage={t("noClientsYet")}
          columns={[
            { header: t("colPhone"), render: (c) => c.phoneNumber, className: "mono" },
            { header: t("colName"), render: (c) => c.name ?? "—" },
            {
              header: t("colStatus"),
              render: (c) =>
                c.blackListed ? (
                  <span title={c.blackListReason ?? undefined}>
                    <Pill variant="critical">{t("blackListed")}</Pill>
                  </span>
                ) : (
                  <Pill variant="success">{t("statusActive")}</Pill>
                ),
            },
            { header: t("colCreated"), render: (c) => new Date(c.createdAt).toLocaleDateString(), className: "mono" },
            {
              header: "",
              render: (c) => (
                <span className="row-actions">
                  <button className="link" onClick={() => setEditing(c)}>
                    {t("edit")}
                  </button>
                  {c.blackListed ? (
                    <button
                      className="link"
                      onClick={async () => {
                        await undoBlackListClient(token!, c.id);
                        setRefreshKey((k) => k + 1);
                      }}
                    >
                      {t("undoBlackList")}
                    </button>
                  ) : (
                    <button className="link danger" onClick={() => setBlacklisting(c)}>
                      {t("blackList")}
                    </button>
                  )}
                  <button
                    className="link danger"
                    onClick={async () => {
                      await removeClient(token!, c.id);
                      setRefreshKey((k) => k + 1);
                    }}
                  >
                    {t("remove")}
                  </button>
                </span>
              ),
            },
          ]}
        />
      </Card>

      {editing && (
        <ClientEditor
          existing={editing === "new" ? undefined : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
      {blacklisting && (
        <BlackListPanel
          client={blacklisting}
          onClose={() => setBlacklisting(null)}
          onSaved={() => {
            setBlacklisting(null);
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
    </AppShell>
  );
}

function ClientEditor({
  existing,
  onClose,
  onSaved,
}: {
  existing?: ClientResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [phoneNumber, setPhoneNumber] = useState(existing?.phoneNumber ?? "");
  const [name, setName] = useState(existing?.name ?? "");
  const [error, setError] = useState<string | undefined>();
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!isRequired(phoneNumber)) {
      setError(t("phoneNumberRequired"));
      return;
    }
    setSubmitting(true);
    try {
      const request = { phoneNumber, name: name || null };
      if (existing) {
        await updateClient(token!, existing.id, request);
      } else {
        await createClient(token!, request);
      }
      onSaved();
    } catch (err) {
      // 409 = duplicate phone number (DuplicateClientPhoneNumberException).
      setError(err instanceof ApiError && err.status === 409 ? t("duplicatePhoneNumber") : t("somethingWentWrong"));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <div className="backdrop" onClick={onClose} />
      <div className="panel">
        <div className="panel-header">
          <h1>{existing ? t("editClient") : t("addClient")}</h1>
          <button className="close-btn" onClick={onClose}>
            ✕
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <TextField
            label={t("colPhone")}
            value={phoneNumber}
            onChange={(e) => setPhoneNumber(e.target.value)}
            error={error}
            placeholder="+994 XX XXX XX XX"
          />
          <TextField label={t("name")} value={name} onChange={(e) => setName(e.target.value)} />
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

function BlackListPanel({
  client,
  onClose,
  onSaved,
}: {
  client: ClientResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await blackListClient(token!, client.id, { reason: reason || null });
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
          <h1>
            {t("blackList")} — {client.name ?? client.phoneNumber}
          </h1>
          <button className="close-btn" onClick={onClose}>
            ✕
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <TextField label={t("blackListReason")} value={reason} onChange={(e) => setReason(e.target.value)} />
          <div className="note" style={{ marginBottom: "var(--space-3)" }}>
            {t("blackListNote")}
          </div>
          <div className="actions">
            <Button type="submit" variant="danger" loading={submitting}>
              {t("blackList")}
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
