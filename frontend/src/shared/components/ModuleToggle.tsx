import { Pill } from "@/shared/components/Pill";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { moduleLabels, translateEnum } from "@/shared/i18n/translations";
import { findModule } from "@/modules/registry";
import type { Module } from "@/shared/api/types";

/** One module, on or off. Used by both admin screens so a grant looks and behaves the same
 *  whether the tenant is being created or edited.
 *
 * A bordered row rather than a bare checkbox and a word: this is the control that decides what
 * a paying client can use, and it was reading as loose text on the page. */
export function ModuleToggle({
  module,
  checked,
  disabled,
  onChange,
}: {
  module: Module;
  checked: boolean;
  disabled?: boolean;
  onChange: (next: boolean) => void;
}) {
  const { language, t } = useLanguage();

  // A module the front end cannot render yet would drop the tenant into a picker holding a tile
  // that goes nowhere. The row stays — the grant is a real record — but it cannot be set here.
  const built = findModule(module) !== undefined;
  const locked = disabled || !built;

  return (
    <label
      style={{
        display: "flex",
        alignItems: "center",
        gap: "var(--space-3)",
        padding: "var(--space-3)",
        marginBottom: "var(--space-2)",
        border: "1px solid var(--color-border)",
        borderRadius: "var(--radius-md)",
        background: checked ? "var(--color-surface-raised)" : "transparent",
        cursor: locked ? "not-allowed" : "pointer",
        opacity: locked ? 0.55 : 1,
      }}
    >
      <input
        type="checkbox"
        checked={checked}
        disabled={locked}
        onChange={(e) => onChange(e.target.checked)}
      />
      <span style={{ flex: 1, fontWeight: checked ? 600 : 400 }}>
        {translateEnum(moduleLabels, module, language)}
      </span>
      {!built && <Pill variant="neutral">{t("moduleNotBuiltYet")}</Pill>}
    </label>
  );
}
