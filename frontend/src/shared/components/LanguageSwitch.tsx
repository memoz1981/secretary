import { useLanguage } from "@/shared/i18n/LanguageContext";
import { LANGUAGES, type Language } from "@/shared/i18n/translations";

/** Every language, always visible, with the active one filled in.
 *
 * It replaces a single button labelled "AZ ▾ (RU)", which asked the reader to work out whether
 * that meant "you are in Azerbaijani" or "switch to Azerbaijani" — the two readings are
 * opposite, and there was no way to tell which one was true. A segmented control has no such
 * ambiguity: the highlighted segment is the current language and the others are the switch.
 *
 * Built on `.view-toggle` so it matches the calendar, dashboard and call-mode pickers.
 */

/** Codes, not endonyms: three segments reading "Azərbaycanca / Русский / English" would not fit
 *  a header. Typed against Language so a new language fails the build here rather than
 *  rendering an empty segment. */
const LABELS: Record<Language, string> = {
  az: "AZ",
  ru: "RU",
  en: "EN",
};

export function LanguageSwitch() {
  const { language, setLanguage } = useLanguage();

  return (
    <div className="view-toggle lang-toggle" role="group" aria-label="Language / Dil / Язык">
      {LANGUAGES.map((option) => (
        <span
          key={option}
          role="button"
          tabIndex={0}
          lang={option}
          aria-pressed={language === option}
          className={language === option ? "active" : ""}
          onClick={() => setLanguage(option)}
          // Spans are not focusable or keyboard-operable on their own; the picker would
          // otherwise be unreachable without a mouse.
          onKeyDown={(event) => {
            if (event.key === "Enter" || event.key === " ") {
              event.preventDefault();
              setLanguage(option);
            }
          }}
        >
          {LABELS[option]}
        </span>
      ))}
    </div>
  );
}
