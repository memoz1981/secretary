import { useState, type FormEvent } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Button } from "@/shared/components/Button";
import { DataTable } from "@/shared/components/DataTable";
import { TextField, SelectField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { getCurrentTenant, updateCurrentTenant } from "@/shared/api/tenants";
import {
  addStaffAccount,
  getStaffAccounts,
  removeStaffAccount,
  resetStaffAccountPassword,
  updateStaffAccount,
} from "@/shared/api/accounts";
import type { AccountResponse } from "@/shared/api/types";
import { ThemePicker } from "@/shared/components/ThemeSelect";
import { BusinessHoursCard } from "@/shared/components/BusinessHoursCard";
import { isRequired, isValidEmail, isMinLength } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { accountRoleLabels, translateEnum } from "@/shared/i18n/translations";

export function AdminPage() {
  const { token, role } = useAuth();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();
  const [refreshKey, setRefreshKey] = useState(0);
  const tenantState = useApiData(() => getCurrentTenant(token!), [token, refreshKey]);
  const accountsState = useApiData(() => getStaffAccounts(token!), [token, refreshKey]);

  const [form, setForm] = useState<{ name: string; timezone: string; phoneLine: string } | null>(null);
  const [savingTenant, setSavingTenant] = useState(false);
  const [addingStaff, setAddingStaff] = useState(false);
  const [editingAccount, setEditingAccount] = useState<AccountResponse | null>(null);
  const [resettingAccount, setResettingAccount] = useState<AccountResponse | null>(null);

  const tenant = tenantState.status === "success" ? tenantState.data : null;
  if (tenant && !form) {
    setForm({ name: tenant.name, timezone: tenant.timezone, phoneLine: tenant.phoneLine ?? "" });
  }

  const accounts = accountsState.status === "success" ? accountsState.data : [];

  async function handleSaveTenant(e: FormEvent) {
    e.preventDefault();
    if (!form || !isRequired(form.name)) return;
    setSavingTenant(true);
    try {
      await updateCurrentTenant(token!, { name: form.name, timezone: form.timezone, phoneLine: form.phoneLine || null });
      setRefreshKey((k) => k + 1);
    } finally {
      setSavingTenant(false);
    }
  }

  return (
    <AppShell {...shell}>
      <h1 className="page-title" style={{ marginBottom: "var(--space-5)" }}>
        {t("adminTitle")}
      </h1>

      <Card>
        <h2 style={{ marginBottom: "var(--space-3)" }}>{t("businessDetails")}</h2>
        {tenantState.status === "error" && <div className="field-error">{t("failedToLoadTenant")}</div>}
        {form && (
          <form onSubmit={handleSaveTenant}>
            <TextField label={t("businessName")} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <SelectField
              label={t("timezone")}
              value={form.timezone}
              onChange={(e) => setForm({ ...form, timezone: e.target.value })}
              options={[{ value: "Asia/Baku", label: "Asia/Baku" }]}
            />
            <TextField label={t("phoneLine")} value={form.phoneLine} onChange={(e) => setForm({ ...form, phoneLine: e.target.value })} />
            <div className="actions">
              <Button type="submit" loading={savingTenant}>
                {t("saveChanges")}
              </Button>
            </div>
          </form>
        )}
      </Card>

      <BusinessHoursCard canEdit={role === "Owner"} />

      <Card>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-3)" }}>
          <h2>{t("staffAccounts")}</h2>
          <Button size="sm" onClick={() => setAddingStaff(true)}>
            {t("addStaff")}
          </Button>
        </div>
        {accountsState.status === "error" && <div className="field-error">{t("failedToLoadAccounts")}</div>}
        <DataTable
          loading={accountsState.status === "loading"}
          rows={accounts}
          rowKey={(a) => a.id}
          emptyMessage={t("noStaffAccountsYet")}
          columns={[
            { header: t("colName"), render: (a) => a.name },
            { header: t("colEmail"), render: (a) => a.email },
            { header: t("colRole"), render: (a) => translateEnum(accountRoleLabels, a.role, language) },
            {
              header: "",
              render: (a) => (
                <span className="row-actions">
                  <button className="link" onClick={() => setEditingAccount(a)}>
                    {t("edit")}
                  </button>
                  <button className="link" onClick={() => setResettingAccount(a)}>
                    {t("resetPassword")}
                  </button>
                  {a.role !== "Owner" && (
                    <button
                      className="link danger"
                      onClick={async () => {
                        await removeStaffAccount(token!, a.id);
                        setRefreshKey((k) => k + 1);
                      }}
                    >
                      {t("remove")}
                    </button>
                  )}
                </span>
              ),
            },
          ]}
        />
      </Card>

      <Card>
        <h2 style={{ marginBottom: "var(--space-3)" }}>{t("appearance")}</h2>
        <ThemePicker />
      </Card>

      {addingStaff && (
        <AddStaffPanel
          onClose={() => setAddingStaff(false)}
          onSaved={() => {
            setAddingStaff(false);
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
      {editingAccount && (
        <EditAccountPanel
          account={editingAccount}
          onClose={() => setEditingAccount(null)}
          onSaved={() => {
            setEditingAccount(null);
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
      {resettingAccount && (
        <ResetPasswordPanel
          account={resettingAccount}
          onClose={() => setResettingAccount(null)}
          onSaved={() => setResettingAccount(null)}
        />
      )}
    </AppShell>
  );
}

function AddStaffPanel({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const nextErrors: Record<string, string> = {};
    if (!isRequired(name)) nextErrors.name = t("nameRequired");
    if (!isValidEmail(email)) nextErrors.email = t("enterValidEmail");
    if (!isMinLength(password, 8)) nextErrors.password = t("atLeast8Characters");
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    setSubmitting(true);
    try {
      await addStaffAccount(token!, { name, email, password });
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
          <h1>{t("addStaff")}</h1>
          <button className="close-btn" onClick={onClose}>
            ✕
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <TextField label={t("name")} value={name} onChange={(e) => setName(e.target.value)} error={errors.name} />
          <TextField label={t("email")} value={email} onChange={(e) => setEmail(e.target.value)} error={errors.email} />
          <TextField label={t("password")} type="password" value={password} onChange={(e) => setPassword(e.target.value)} error={errors.password} />
          <div className="actions">
            <Button type="submit" loading={submitting}>
              {t("add")}
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

function EditAccountPanel({
  account,
  onClose,
  onSaved,
}: {
  account: AccountResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [name, setName] = useState(account.name);
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
      await updateStaffAccount(token!, account.id, { name });
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
          <h1>{t("editAccount")}</h1>
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

function ResetPasswordPanel({
  account,
  onClose,
  onSaved,
}: {
  account: AccountResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | undefined>();
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!isMinLength(password, 8)) {
      setError(t("atLeast8Characters"));
      return;
    }
    setSubmitting(true);
    try {
      await resetStaffAccountPassword(token!, account.id, { password });
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
            {t("resetPassword")} — {account.name}
          </h1>
          <button className="close-btn" onClick={onClose}>
            ✕
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <TextField
            label={t("newPassword")}
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={error}
          />
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
