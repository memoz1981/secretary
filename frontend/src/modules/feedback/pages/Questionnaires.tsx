import { useState, type FormEvent } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { Button } from "@/shared/components/Button";
import { Card } from "@/shared/components/Card";
import { DataTable } from "@/shared/components/DataTable";
import { SelectField, TextField } from "@/shared/components/FormControls";
import { SidePanel } from "@/shared/components/SidePanel";
import { Pill } from "@/shared/components/Pill";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { isRequired } from "@/shared/lib/validation";
import {
  addQuestion,
  createSurvey,
  getSurvey,
  getSurveyQuota,
  getSurveys,
  removeQuestion,
  removeSurvey,
  setRetryPolicy,
  reorderQuestions,
  updateQuestion,
} from "@/modules/feedback/api/feedback";
import { SCALE_MAXIMUMS } from "@/shared/api/types";
import type { FeedbackQuestionType, SurveyQuestionResponse, SurveyResponse } from "@/shared/api/types";
import type { TranslationKey } from "@/shared/i18n/translations";

/** One label per type, in one place, so the list and the editor cannot drift apart. */
const QUESTION_TYPE_LABEL: Record<FeedbackQuestionType, TranslationKey> = {
  YesNo: "yesNoQuestion",
  Scale: "scaleQuestion",
  Choice: "multipleChoice",
  Open: "openQuestion",
};

/** The questionnaires and what is in them.
 *
 * Two levels on one page: the list of questionnaires, and — once one is picked — its questions in
 * the order they will be asked. Order matters here in a way it does not on the other editing
 * screens, because it is literally the sequence of a conversation. */
export function QuestionnairesPage() {
  const { token, role } = useAuth();
  const { t } = useLanguage();
  const shell = useBusinessShell();
  const canEdit = role === "Owner";

  const [refreshKey, setRefreshKey] = useState(0);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [namingNew, setNamingNew] = useState(false);
  const [editingQuestion, setEditingQuestion] = useState<SurveyQuestionResponse | "new" | null>(null);
  const [editingRetries, setEditingRetries] = useState<SurveyResponse | null>(null);

  const surveys = useApiData(() => getSurveys(token!), [token, refreshKey]);
  const quota = useApiData(() => getSurveyQuota(token!), [token, refreshKey]);
  const detail = useApiData(
    () => (selectedId === null ? Promise.resolve(null) : getSurvey(token!, selectedId)),
    [token, selectedId, refreshKey],
  );

  const rows = surveys.status === "success" ? surveys.data : [];
  const survey = detail.status === "success" ? detail.data : null;
  const limit = quota.status === "success" ? quota.data : null;
  const atLimit = limit !== null && limit.usedSurveys >= limit.maxSurveys;

  function refresh() {
    setRefreshKey((k) => k + 1);
  }

  async function move(question: SurveyQuestionResponse, delta: number) {
    if (survey === null) return;
    const ids = survey.questions.map((q) => q.id);
    const from = ids.indexOf(question.id);
    const to = from + delta;
    if (to < 0 || to >= ids.length) return;

    [ids[from], ids[to]] = [ids[to], ids[from]];
    await reorderQuestions(token!, survey.id, ids);
    refresh();
  }

  return (
    <AppShell {...shell}>
      <h1 className="page-title">{t("questionnaires")}</h1>
      <div className="subtitle">{canEdit ? t("ownerViewEditable") : t("staffViewReadOnly")}</div>

      <Card>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: "var(--space-3)",
          }}
        >
          <h2>{t("questionnaires")}</h2>
          <div style={{ display: "flex", gap: "var(--space-3)", alignItems: "center" }}>
            {/* The quota shown as used-of-allowed, not only enforced when the next one is
                refused — an Owner should know where they stand before they try. */}
            {limit && (
              <span className="mono" style={{ color: "var(--color-muted)", fontSize: "var(--text-sm)" }}>
                {limit.usedSurveys} / {limit.maxSurveys}
              </span>
            )}
            {canEdit && (
              <Button size="sm" disabled={atLimit} onClick={() => setNamingNew(true)}>
                {t("addQuestionnaire")}
              </Button>
            )}
          </div>
        </div>

        {atLimit && <div className="note">{t("questionnaireQuotaReached")}</div>}

        <DataTable
          loading={surveys.status === "loading"}
          rows={rows}
          rowKey={(s) => s.id}
          emptyMessage={t("noQuestionnairesYet")}
          onRowClick={(s) => setSelectedId(s.id)}
          columns={[
            { header: t("name"), render: (s) => s.name },
            { header: t("colQuestions"), render: (s) => String(s.questionCount), className: "mono" },
            {
              header: t("retries"),
              render: (s) =>
                s.retryCount === 0
                  ? t("noRetries")
                  : t("retryPolicySummary")
                      .replace("{n}", String(s.retryCount))
                      .replace("{m}", String(s.retryDelayMinutes)),
              className: "mono",
            },
            ...(canEdit
              ? [
                  {
                    header: "",
                    render: (s: SurveyResponse) => (
                      <span className="row-actions">
                        <button
                          className="link"
                          onClick={(e) => {
                            e.stopPropagation();
                            setEditingRetries(s);
                          }}
                        >
                          {t("retries")}
                        </button>
                        <button
                          className="link danger"
                          onClick={async (e) => {
                            e.stopPropagation();
                            try {
                              await removeSurvey(token!, s.id);
                              if (selectedId === s.id) setSelectedId(null);
                              refresh();
                            } catch {
                              // Already on screen: ApiFailureBanner reports every refused write.
                            }
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

      {survey && (
        <Card>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: "var(--space-3)",
            }}
          >
            <h2>{survey.name}</h2>
            {canEdit && (
              <Button size="sm" onClick={() => setEditingQuestion("new")}>
                {t("addQuestion")}
              </Button>
            )}
          </div>

          <DataTable
            loading={detail.status === "loading"}
            rows={survey.questions}
            rowKey={(q) => q.id}
            emptyMessage={t("noQuestionsYet")}
            columns={[
              { header: "#", render: (q) => String(q.position + 1), className: "mono" },
              {
                header: t("colQuestion"),
                render: (q) => (
                  <span>
                    {q.text}
                    {q.countsTowardScore && (
                      <>
                        {" "}
                        <Pill variant="success">{t("inTheScore")}</Pill>
                      </>
                    )}
                  </span>
                ),
              },
              { header: t("colType"), render: (q) => t(QUESTION_TYPE_LABEL[q.questionType]) },
              {
                // The percentages are shown because they are derived and nobody typed them —
                // seeing 0 / 25 / 50 / 75 / 100 beside a 1-5 is how you know the scale runs the
                // way round you meant.
                header: t("colOptions"),
                render: (q) =>
                  q.options.length === 0
                    ? "—"
                    : q.options
                        .map((o) => (o.scorePercent === null ? o.text : `${o.text} (${o.scorePercent}%)`))
                        .join(", "),
              },
              ...(canEdit
                ? [
                    {
                      header: "",
                      render: (q: SurveyQuestionResponse) => (
                        <span className="row-actions">
                          <button className="link" onClick={() => move(q, -1)} disabled={q.position === 0}>
                            ↑
                          </button>
                          <button
                            className="link"
                            onClick={() => move(q, 1)}
                            disabled={q.position === survey.questions.length - 1}
                          >
                            ↓
                          </button>
                          <button className="link" onClick={() => setEditingQuestion(q)}>
                            {t("edit")}
                          </button>
                          <button
                            className="link danger"
                            onClick={async () => {
                              try {
                                await removeQuestion(token!, q.id);
                                refresh();
                              } catch {
                                // The banner has it — a question somebody has answered cannot go.
                              }
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
      )}

      {namingNew && (
        <NameQuestionnaire
          onClose={() => setNamingNew(false)}
          onSaved={(id) => {
            setNamingNew(false);
            setSelectedId(id);
            refresh();
          }}
        />
      )}

      {editingRetries && (
        <RetryPolicyEditor
          survey={editingRetries}
          onClose={() => setEditingRetries(null)}
          onSaved={() => {
            setEditingRetries(null);
            refresh();
          }}
        />
      )}

      {editingQuestion !== null && survey && (
        <QuestionEditor
          surveyId={survey.id}
          existing={editingQuestion === "new" ? undefined : editingQuestion}
          onClose={() => setEditingQuestion(null)}
          onSaved={() => {
            setEditingQuestion(null);
            refresh();
          }}
        />
      )}
    </AppShell>
  );
}

function NameQuestionnaire({ onClose, onSaved }: { onClose: () => void; onSaved: (id: number) => void }) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [name, setName] = useState("");
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
      const created = await createSurvey(token!, { name });
      onSaved(created.id);
    } catch {
      // The banner reports it — a quota refusal says how many they are allowed.
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <SidePanel title={t("addQuestionnaire")} onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <TextField label={t("name")} value={name} onChange={(e) => setName(e.target.value)} error={error} />
        <div className="panel-actions">
          <Button type="submit" disabled={submitting}>
            {t("save")}
          </Button>
        </div>
      </form>
    </SidePanel>
  );
}

/** One question.
 *
 * ⚠ Nothing numeric is typed here except the size of a scale. The version this replaces had a
 * number box per option, and it produced one live questionnaire scored 20/40/60/80/100 and
 * another auto-numbered into Bəli=1, Xeyr=2 — so "no" outscored "yes". Yes/No and Scale now build
 * their own options and their own percentages, and the only thing the owner writes for them is
 * the question itself.
 *
 * Which fields appear follows the type, so a form for a Yes/No cannot be filled in wrongly rather
 * than being filled in wrongly and rejected. */
function QuestionEditor({
  surveyId,
  existing,
  onClose,
  onSaved,
}: {
  surveyId: number;
  existing?: SurveyQuestionResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [text, setText] = useState(existing?.text ?? "");
  const [type, setType] = useState<FeedbackQuestionType>(existing?.questionType ?? "YesNo");
  const [countsTowardScore, setCountsTowardScore] = useState(existing?.countsTowardScore ?? true);
  const [scaleMax, setScaleMax] = useState(existing?.scaleMax ?? 5);
  const [yesIsPositive, setYesIsPositive] = useState(existing?.yesIsPositive ?? true);
  const [allowOther, setAllowOther] = useState(existing?.allowOther ?? false);
  const [labels, setLabels] = useState<string[]>(
    existing?.options.filter((o) => !o.isOther).map((o) => o.text) ?? ["", ""],
  );

  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const scored = type === "YesNo" || type === "Scale";

  function setLabel(index: number, value: string) {
    setLabels((current) => current.map((o, i) => (i === index ? value : o)));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const next: Record<string, string> = {};
    const filled = labels.filter(isRequired);

    if (!isRequired(text)) next.text = t("questionTextRequired");
    if (type === "Choice" && filled.length < 2) next.options = t("atLeastTwoOptions");

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSubmitting(true);
    try {
      const request = {
        text,
        questionType: type,
        // Only the two types with an ordering can contribute — the server refuses the rest, and
        // sending it anyway would make the tick look like it had been ignored.
        countsTowardScore: scored && countsTowardScore,
        scaleMax: type === "Scale" ? scaleMax : null,
        yesIsPositive,
        allowOther: type === "Choice" && allowOther,
        labels: type === "Choice" ? filled : [],
      };

      if (existing) {
        await updateQuestion(token!, existing.id, request);
      } else {
        await addQuestion(token!, surveyId, request);
      }

      onSaved();
    } catch {
      // The banner reports it — editing an answered question is refused with the reason.
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <SidePanel title={existing ? t("editQuestion") : t("addQuestion")} onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <TextField
          label={t("colQuestion")}
          value={text}
          onChange={(e) => setText(e.target.value)}
          error={errors.text}
        />
        <SelectField
          label={t("colType")}
          value={type}
          options={[
            { value: "YesNo", label: t("yesNoQuestion") },
            { value: "Scale", label: t("scaleQuestion") },
            { value: "Choice", label: t("multipleChoice") },
            { value: "Open", label: t("openQuestion") },
          ]}
          onChange={(e) => setType(e.target.value as FeedbackQuestionType)}
        />

        {type === "YesNo" && (
          <>
            {/* "Gözləmə uzun oldu?" is a question you want answered no. Without this its score
                runs backwards and a well-run service reads as a failing one. */}
            <SelectField
              label={t("goodAnswer")}
              value={yesIsPositive ? "yes" : "no"}
              options={[
                { value: "yes", label: t("yesIsGood") },
                { value: "no", label: t("noIsGood") },
              ]}
              onChange={(e) => setYesIsPositive(e.target.value === "yes")}
            />
            <div className="note">{t("yesNoExplain")}</div>
          </>
        )}

        {type === "Scale" && (
          <>
            <SelectField
              label={t("scaleTop")}
              value={String(scaleMax)}
              options={SCALE_MAXIMUMS.map((n) => ({ value: String(n), label: `1 – ${n}` }))}
              onChange={(e) => setScaleMax(Number(e.target.value))}
            />
            <div className="note">{t("scaleExplain")}</div>
          </>
        )}

        {type === "Choice" && (
          <>
            <div className="section-label">{t("colOptions")}</div>
            {labels.map((option, index) => (
              <div key={index} style={{ display: "flex", gap: "var(--space-2)", alignItems: "flex-end" }}>
                <TextField
                  label="•"
                  value={option}
                  onChange={(e) => setLabel(index, e.target.value)}
                  className="grow"
                />
                <button
                  type="button"
                  className="link danger"
                  onClick={() => setLabels((c) => c.filter((_, i) => i !== index))}
                >
                  ✕
                </button>
              </div>
            ))}
            {errors.options && <div className="field-error">{errors.options}</div>}
            <div style={{ marginTop: "var(--space-2)" }}>
              <button type="button" className="link" onClick={() => setLabels((c) => [...c, ""])}>
                + {t("addOption")}
              </button>
            </div>

            <label className="checkbox-row" style={{ marginTop: "var(--space-4)" }}>
              <input type="checkbox" checked={allowOther} onChange={(e) => setAllowOther(e.target.checked)} />
              <span>{t("allowOther")}</span>
            </label>
            <div className="note">{t("allowOtherExplain")}</div>
          </>
        )}

        {scored && (
          <>
            <label className="checkbox-row" style={{ marginTop: "var(--space-4)" }}>
              <input
                type="checkbox"
                checked={countsTowardScore}
                onChange={(e) => setCountsTowardScore(e.target.checked)}
              />
              <span>{t("countsTowardScore")}</span>
            </label>
            <div className="note">{t("countsTowardScoreExplain")}</div>
          </>
        )}

        <div className="panel-actions">
          <Button type="submit" disabled={submitting}>
            {t("save")}
          </Button>
        </div>
      </form>
    </SidePanel>
  );
}

/** How hard to chase somebody who never answered.
 *
 * ⚠ Applies to nobody who did. A caller who picked up and could not be understood is handed to a
 * person, and dialling them again with the same agent would fail identically — a second identical
 * call is how a survey becomes a nuisance. Said on the panel, because the setting reads as if it
 * governs every failed call and it does not.
 *
 * Both fields are pickers rather than number boxes. This module has already shipped one free
 * numeric input that produced a live questionnaire scoring Bəli=1, Xeyr=2. */
function RetryPolicyEditor({
  survey,
  onClose,
  onSaved,
}: {
  survey: SurveyResponse;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { token } = useAuth();
  const { t } = useLanguage();
  const [retryCount, setRetryCount] = useState(survey.retryCount);
  const [delay, setDelay] = useState(survey.retryDelayMinutes);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await setRetryPolicy(token!, survey.id, { retryCount, retryDelayMinutes: delay });
      onSaved();
    } catch {
      // The banner has it.
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <SidePanel title={t("retries")} onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <SelectField
          label={t("retryCount")}
          value={String(retryCount)}
          options={[0, 1, 2, 3, 4, 5].map((n) => ({
            value: String(n),
            label: n === 0 ? t("noRetries") : String(n),
          }))}
          onChange={(e) => setRetryCount(Number(e.target.value))}
        />
        {retryCount > 0 && (
          <SelectField
            label={t("retryDelay")}
            value={String(delay)}
            options={[15, 30, 60, 180, 360, 1440, 2880].map((m) => ({
              value: String(m),
              label: m < 60 ? `${m} dəq` : m < 1440 ? `${m / 60} saat` : `${m / 1440} gün`,
            }))}
            onChange={(e) => setDelay(Number(e.target.value))}
          />
        )}
        <div className="note">{t("retriesExplain")}</div>
        <div className="note">{t("scheduledDialsWaitForTelephony")}</div>
        <div className="panel-actions">
          <Button type="submit" disabled={submitting}>
            {t("save")}
          </Button>
        </div>
      </form>
    </SidePanel>
  );
}
