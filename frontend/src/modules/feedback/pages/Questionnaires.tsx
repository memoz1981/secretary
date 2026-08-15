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
  reorderQuestions,
  updateQuestion,
} from "@/modules/feedback/api/feedback";
import type {
  FeedbackQuestionType,
  SaveOptionRequest,
  SurveyQuestionResponse,
  SurveyResponse,
} from "@/shared/api/types";

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
            ...(canEdit
              ? [
                  {
                    header: "",
                    render: (s: SurveyResponse) => (
                      <span className="row-actions">
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
                    {q.isHeadline && (
                      <>
                        {" "}
                        <Pill variant="success">{t("headline")}</Pill>
                      </>
                    )}
                  </span>
                ),
              },
              {
                header: t("colType"),
                render: (q) =>
                  q.questionType === "Open"
                    ? t("openQuestion")
                    : q.isScored
                      ? t("scoredChoice")
                      : t("multipleChoice"),
              },
              {
                header: t("colOptions"),
                render: (q) =>
                  q.options.length === 0
                    ? "—"
                    : q.options.map((o) => (o.value === null ? o.text : `${o.text} (${o.value})`)).join(", "),
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

/** One question and its options.
 *
 * The rule the form has to carry: every option numbered, or none. A partly numbered question
 * would average some answers and drop the rest, which is worse than not averaging — so the
 * server refuses it, and saying so here saves a round trip. */
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
  const [type, setType] = useState<FeedbackQuestionType>(existing?.questionType ?? "Choice");
  const [isHeadline, setIsHeadline] = useState(existing?.isHeadline ?? false);
  const [options, setOptions] = useState<SaveOptionRequest[]>(
    existing?.options.map((o) => ({ text: o.text, value: o.value })) ?? [
      { text: "", value: null },
      { text: "", value: null },
    ],
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  function setOption(index: number, patch: Partial<SaveOptionRequest>) {
    setOptions((current) => current.map((o, i) => (i === index ? { ...o, ...patch } : o)));
  }

  /** Numbers 1..n down the list, which is what a rating scale is and what people would
   *  otherwise type by hand and get wrong. */
  function numberThem() {
    setOptions((current) => current.map((o, i) => ({ ...o, value: i + 1 })));
  }

  function clearNumbers() {
    setOptions((current) => current.map((o) => ({ ...o, value: null })));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const next: Record<string, string> = {};
    const filled = type === "Choice" ? options.filter((o) => isRequired(o.text)) : [];

    if (!isRequired(text)) next.text = t("questionTextRequired");
    if (type === "Choice" && filled.length < 2) next.options = t("atLeastTwoOptions");

    const valued = filled.filter((o) => o.value !== null).length;
    if (valued !== 0 && valued !== filled.length) next.options = t("numberAllOptionsOrNone");
    if (type === "Open" && isHeadline) next.headline = t("headlineMustBeChoice");

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSubmitting(true);
    try {
      const request = { text, questionType: type, isHeadline, options: filled };
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
            { value: "Choice", label: t("multipleChoice") },
            { value: "Open", label: t("openQuestion") },
          ]}
          onChange={(e) => setType(e.target.value as FeedbackQuestionType)}
        />

        {type === "Choice" && (
          <>
            <div className="section-label">{t("colOptions")}</div>
            {options.map((option, index) => (
              <div key={index} style={{ display: "flex", gap: "var(--space-2)", alignItems: "flex-end" }}>
                <TextField
                  label={`${index + 1}`}
                  value={option.text}
                  onChange={(e) => setOption(index, { text: e.target.value })}
                  className="grow"
                />
                <TextField
                  label={t("colValue")}
                  value={option.value === null ? "" : String(option.value)}
                  style={{ width: "72px" }}
                  onChange={(e) =>
                    setOption(index, { value: e.target.value.trim() === "" ? null : Number(e.target.value) })
                  }
                />
                <button
                  type="button"
                  className="link danger"
                  onClick={() => setOptions((c) => c.filter((_, i) => i !== index))}
                >
                  ✕
                </button>
              </div>
            ))}
            {errors.options && <div className="field-error">{errors.options}</div>}
            <div style={{ display: "flex", gap: "var(--space-3)", marginTop: "var(--space-2)" }}>
              <button type="button" className="link" onClick={() => setOptions((c) => [...c, { text: "", value: null }])}>
                + {t("addOption")}
              </button>
              <button type="button" className="link" onClick={numberThem}>
                {t("numberOptions")}
              </button>
              <button type="button" className="link" onClick={clearNumbers}>
                {t("clearNumbers")}
              </button>
            </div>
            <div className="note">{t("numberedOptionsExplain")}</div>

            <label className="checkbox-row" style={{ marginTop: "var(--space-4)" }}>
              <input type="checkbox" checked={isHeadline} onChange={(e) => setIsHeadline(e.target.checked)} />
              <span>{t("useAsHeadline")}</span>
            </label>
            {errors.headline && <div className="field-error">{errors.headline}</div>}
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
