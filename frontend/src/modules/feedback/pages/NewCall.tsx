import { useState, type FormEvent } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Button } from "@/shared/components/Button";
import { Card } from "@/shared/components/Card";
import { SelectField, TextField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { isRequired } from "@/shared/lib/validation";
import { getSurveys, queueFeedbackCall } from "@/modules/feedback/api/feedback";
import { formatAzPhone, isValidAzPhone, toApiPhone } from "@/modules/feedback/api/phone";
import { LiveVoiceCall } from "@/shared/lib/liveVoiceCall";

type Status = "idle" | "queueing" | "connecting" | "inCall" | "ended";

/** Ring somebody and ask them the questions.
 *
 * ⚠ The form is standing in for a scheduler. The point of taking the name and number *before*
 * the call rather than discovering them during it is that this is the shape outbound has: the
 * system knows who it is calling before anybody speaks. When telephony lands, a campaign posts
 * the same request and something dials the number instead of a browser opening a microphone —
 * and the agent, the tools and the answers are untouched.
 *
 * So the number is not decorative even though nothing dials it today. It is the field the
 * scheduler will use, and it is what makes the demo read as a real follow-up. */
export function NewFeedbackCallPage() {
  const { token } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();

  const surveys = useApiData(() => getSurveys(token!), [token]);
  const options = surveys.status === "success" ? surveys.data.filter((s) => s.questionCount > 0) : [];

  const [surveyId, setSurveyId] = useState("");
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<Status>("idle");
  const [call, setCall] = useState<LiveVoiceCall | null>(null);
  const [queued, setQueued] = useState<{ id: number; personName: string } | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const next: Record<string, string> = {};
    if (!isRequired(surveyId)) next.survey = t("chooseQuestionnaire");
    if (!isRequired(name)) next.name = t("nameRequired");
    if (!isValidAzPhone(phone)) next.phone = t("enterValidPhone");

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setStatus("queueing");
    try {
      // The row first, then the call. The agent is handed an id, never a name — answers filed
      // against the wrong call would appear under somebody else's name.
      const created = await queueFeedbackCall(token!, {
        surveyId: Number(surveyId),
        personName: name.trim(),
        phoneNumber: toApiPhone(phone),
      });

      setQueued({ id: created.id, personName: created.personName });
      setStatus("connecting");

      const session = new LiveVoiceCall(
        import.meta.env.VITE_API_BASE_URL as string,
        token!,
        {
          onStatus: (live) =>
            setStatus(live === "in-call" ? "inCall" : live === "connecting" ? "connecting" : "ended"),
        },
        // Gemini only, and the server refuses anything else for this module rather than
        // trusting the picker — there is one instruction file and it is written for this model.
        "GeminiLive_3_1",
        "Feedback",
        created.id,
      );

      setCall(session);
      await session.start();
    } catch {
      // The banner reports it — a questionnaire with no questions says so.
      setStatus("idle");
    }
  }

  function hangUp() {
    void call?.hangUp();
    setCall(null);
    setStatus("ended");
  }

  const busy = status === "queueing" || status === "connecting" || status === "inCall";

  return (
    <AppShell {...shell}>
      <h1 className="page-title">{t("newFeedbackCall")}</h1>
      <div className="subtitle">{t("newFeedbackCallSubtitle")}</div>

      <Card>
        <form onSubmit={handleSubmit}>
          <SelectField
            label={t("questionnaire")}
            value={surveyId}
            options={options.map((s) => ({ value: String(s.id), label: `${s.name} (${s.questionCount})` }))}
            placeholder={t("chooseQuestionnaire")}
            onChange={(e) => setSurveyId(e.target.value)}
            error={errors.survey}
            disabled={busy}
          />
          <TextField
            label={t("customerName")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            error={errors.name}
            disabled={busy}
          />
          <TextField
            label={t("colPhone")}
            value={phone}
            placeholder="+994(50)250-58-32"
            // Formatted as it is typed rather than corrected afterwards — a field that rewrites
            // itself once you leave it reads as a rejection.
            onChange={(e) => setPhone(formatAzPhone(e.target.value))}
            error={errors.phone}
            disabled={busy}
          />

          <div className="panel-actions">
            {status === "inCall" || status === "connecting" ? (
              <Button type="button" variant="danger" onClick={hangUp}>
                {t("hangUp")}
              </Button>
            ) : (
              <Button type="submit" disabled={busy || options.length === 0}>
                {t("makeCall")}
              </Button>
            )}
          </div>
        </form>

        {options.length === 0 && surveys.status === "success" && (
          <div className="note">{t("needAQuestionnaireFirst")}</div>
        )}

        {queued && status !== "idle" && (
          <div className="note">
            {status === "inCall"
              ? t("callInProgressWith").replace("{name}", queued.personName)
              : status === "ended"
                ? t("callEndedSeeCalls")
                : t("connecting")}
          </div>
        )}
      </Card>
    </AppShell>
  );
}
