import { useEffect, useState } from "react";
import { onApiFailure } from "@/shared/api/client";
import { useLanguage } from "@/shared/i18n/LanguageContext";

/**
 * Says out loud when the server refuses a write.
 *
 * One banner for the whole app rather than error state on every page, because the failure this
 * fixes was pages forgetting — a save or a delete that rejected and left the screen exactly as it
 * was, with the row still sitting there and nothing to read. A page cannot forget to report an
 * error it is not responsible for reporting.
 *
 * The server's own sentence is shown, never a generic apology: "Provider '3' is linked to
 * upcoming appointments and cannot be removed" tells an Owner what to do next, and "Could not
 * remove" sends them hunting. Only when the response carried no message at all does the fallback
 * appear.
 */
export function ApiFailureBanner() {
  const { t } = useLanguage();

  // Undefined is "nothing has failed"; a string — including an empty one — is "something did".
  // Collapsing those two into null meant a refusal that carried no readable sentence showed
  // nothing at all, which is the exact silence this component exists to end.
  const [message, setMessage] = useState<string | undefined>(undefined);

  useEffect(() => onApiFailure((error) => setMessage(error.message?.trim() ?? "")), []);

  if (message === undefined) {
    return null;
  }

  return (
    <div className="api-failure" role="alert">
      <span className="api-failure-text">{message || t("actionFailed")}</span>
      <button
        type="button"
        className="api-failure-close"
        onClick={() => setMessage(undefined)}
        aria-label={t("close")}
      >
        ✕
      </button>
    </div>
  );
}
