import { useLanguage } from "@/shared/i18n/LanguageContext";
import { themeLabels, translateEnum } from "@/shared/i18n/translations";
import { useTheme } from "@/shared/theme/ThemeContext";
import { THEME_IDS, THEME_SWATCHES, type ThemeId } from "@/shared/theme/themes";

/** Compact theme picker — sits in the topbar next to the language switch, and again on the
 * Admin page. Applying is instant and global (a `data-theme` attribute on <html>), so there
 * is nothing to save. */
export function ThemeSelect({ id, className }: { id?: string; className?: string }) {
  const { theme, setTheme } = useTheme();
  const { language, t } = useLanguage();

  return (
    <select
      id={id}
      className={className}
      aria-label={t("theme")}
      value={theme}
      onChange={(e) => setTheme(e.target.value as ThemeId)}
    >
      {THEME_IDS.map((themeId) => (
        <option key={themeId} value={themeId}>
          {translateEnum(themeLabels, themeId, language)}
        </option>
      ))}
    </select>
  );
}

/** Full-size picker for the Admin page: the same choices as the topbar select, but each
 * theme shows its actual palette — page, surface and accent — so the caller isn't picking
 * colors from a name. Selecting applies immediately; there is nothing to save. */
export function ThemePicker() {
  const { theme, setTheme } = useTheme();
  const { language, t } = useLanguage();

  return (
    <>
      <div className="theme-grid">
        {THEME_IDS.map((themeId) => (
          <button
            key={themeId}
            type="button"
            className={`theme-card ${theme === themeId ? "active" : ""}`}
            aria-pressed={theme === themeId}
            onClick={() => setTheme(themeId)}
          >
            <span className="theme-swatch">
              {THEME_SWATCHES[themeId].map((color) => (
                <span className="theme-chip" key={color} style={{ background: color }} aria-hidden="true" />
              ))}
            </span>
            {translateEnum(themeLabels, themeId, language)}
          </button>
        ))}
      </div>
      <div className="note">{t("themeHint")}</div>
    </>
  );
}
