import { useState, type FormEvent } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Button } from "@/shared/components/Button";
import { DataTable } from "@/shared/components/DataTable";
import { SelectField, TextAreaField, TextField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { createProduct, getProducts, getUnits, removeProduct, updateProduct } from "@/modules/orders/api/orders";
import type { ProductDetailResponse } from "@/shared/api/types";
import { isRequired } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function ProductsPage() {
  const { token, role } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useApiData(() => getProducts(token!), [token, refreshKey]);
  const [editing, setEditing] = useState<ProductDetailResponse | "new" | null>(null);

  const canEdit = role === "Owner";
  const products = state.status === "success" ? state.data : [];

  return (
    <AppShell {...shell}>
      <h1 className="page-title">{t("products")}</h1>
      <div className="subtitle">{canEdit ? t("ownerViewEditable") : t("staffViewReadOnly")}</div>
      {canEdit && (
        <div className="toolbar" style={{ justifyContent: "flex-end" }}>
          <Button onClick={() => setEditing("new")}>{t("addProduct")}</Button>
        </div>
      )}
      {state.status === "error" && <div className="field-error">{t("failedToLoadProducts")}</div>}
      <DataTable
        loading={state.status === "loading"}
        rows={products}
        rowKey={(p) => p.id}
        emptyMessage={t("noProductsYet")}
        columns={[
          { header: t("product"), render: (p) => p.name },
          { header: t("colPrice"), render: (p) => `${p.unitPrice} AZN / ${p.unit}`, className: "mono" },
          { header: t("colAliases"), render: (p) => p.aliases ?? "—" },
          ...(canEdit
            ? [
                {
                  header: "",
                  render: (p: ProductDetailResponse) => (
                    <span className="row-actions">
                      <button className="link" onClick={() => setEditing(p)}>
                        {t("edit")}
                      </button>
                      <button
                        className="link danger"
                        onClick={async () => {
                          await removeProduct(token!, p.id);
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
        <ProductEditor
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

function ProductEditor({
  existing,
  onClose,
  onSaved,
}: {
  existing?: ProductDetailResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const units = useApiData(() => getUnits(token!), [token]);
  const [name, setName] = useState(existing?.name ?? "");
  const [description, setDescription] = useState(existing?.description ?? "");
  const [unitId, setUnitId] = useState(existing ? String(existing.measurementUnitId) : "");
  const [price, setPrice] = useState(existing ? String(existing.unitPrice) : "");
  const [aliases, setAliases] = useState(existing?.aliases ?? "");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const unitOptions = units.status === "success" ? units.data.map((u) => ({ value: String(u.id), label: u.name })) : [];

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const nextErrors: Record<string, string> = {};
    if (!isRequired(name)) nextErrors.name = t("productNameRequired");
    if (!isRequired(unitId)) nextErrors.unit = t("unitRequired");
    if (price === "" || Number.isNaN(Number(price)) || Number(price) < 0) nextErrors.price = t("enterValidPrice");
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    setSubmitting(true);
    try {
      const request = {
        name,
        description: description.trim() === "" ? null : description,
        measurementUnitId: Number(unitId),
        unitPrice: Number(price),
        aliases: aliases.trim() === "" ? null : aliases,
      };

      if (existing) {
        await updateProduct(token!, existing.id, request);
      } else {
        await createProduct(token!, request);
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
          <h1>{existing ? t("editProduct") : t("addProduct")}</h1>
          <button className="close-btn" onClick={onClose}>
            ✕
          </button>
        </div>
        <form onSubmit={handleSubmit}>
          <TextField label={t("name")} value={name} onChange={(e) => setName(e.target.value)} error={errors.name} />
          <SelectField
            label={t("unit")}
            value={unitId}
            options={unitOptions}
            placeholder={t("chooseUnit")}
            onChange={(e) => setUnitId(e.target.value)}
            error={errors.unit}
          />
          <TextField label={t("priceAzn")} value={price} onChange={(e) => setPrice(e.target.value)} error={errors.price} />
          <TextAreaField
            label={t("colAliases")}
            value={aliases}
            rows={2}
            onChange={(e) => setAliases(e.target.value)}
          />
          <div className="sub" style={{ marginTop: "calc(var(--space-2) * -1)" }}>{t("aliasesHelp")}</div>
          <TextAreaField
            label={t("description")}
            value={description}
            rows={2}
            onChange={(e) => setDescription(e.target.value)}
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
