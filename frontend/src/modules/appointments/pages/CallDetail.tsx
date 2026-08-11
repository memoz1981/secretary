import { useNavigate, useParams } from "react-router-dom";
import { AppShell } from "@/shared/components/AppShell";
import { Card } from "@/shared/components/Card";
import { Pill } from "@/shared/components/Pill";
import { AudioPlayer } from "@/shared/components/AudioPlayer";
import { Button } from "@/shared/components/Button";
import { useAuth } from "@/shared/auth/AuthContext";
import { useApiData } from "@/shared/lib/useApiData";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { formatDuration, formatTokens, formatUsd } from "@/shared/lib/money";
import { pipelineLabel } from "@/shared/lib/pipelines";
import { getCallDetail } from "@/modules/appointments/api/calls";
import type { CallOutcome, CallResponse } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { formatDayMonthTime } from "@/shared/lib/dates";
import { callClassificationLabels, callOutcomeLabels, translateEnum } from "@/shared/i18n/translations";

function outcomePillVariant(outcome: CallOutcome): "success" | "warning" | "critical" {
  if (outcome === "ResolvedByAgent") return "success";
  if (outcome === "EscalatedResolvedByStaff") return "warning";
  return "critical";
}

/** What this one call cost to run, and where the money went. The per-kind breakdown is the
 *  useful part: audio output is priced 16× audio input, so seeing which line dominates is what
 *  turns "calls are expensive" into something actionable (shorter replies, a cheaper model). */
function CallCostCard({ call }: { call: CallResponse }) {
  const { t } = useLanguage();
  const usage = call.tokenUsage;

  // Calls logged before cost tracking existed carry zeros and no model — saying so is better
  // than showing a confident $0.00 they never actually cost.
  if (!call.agentModel && usage.totalTokens === 0) {
    return (
      <Card>
        <h2>{t("callCost")}</h2>
        <div className="note">{t("costNotTracked")}</div>
      </Card>
    );
  }

  const tokenRows: [string, number][] = [
    [t("tokensAudioIn"), usage.inputAudioTokens],
    [t("tokensCachedAudioIn"), usage.cachedInputAudioTokens],
    [t("tokensAudioOut"), usage.outputAudioTokens],
    [t("tokensTextIn"), usage.inputTextTokens],
    [t("tokensCachedTextIn"), usage.cachedInputTextTokens],
    [t("tokensTextOut"), usage.outputTextTokens],
  ];

  return (
    <Card>
      <h2>{t("callCost")}</h2>
      <div className="field-grid">
        <div className="field">
          <label>{t("callCost")}</label>
          <div className="value mono">{formatUsd(call.costUsd)}</div>
        </div>
        <div className="field">
          <label>{t("costPerMinute")}</label>
          <div className="value mono">{formatUsd(call.costPerMinuteUsd)}</div>
        </div>
        <div className="field">
          <label>{t("costPerAnswer")}</label>
          <div className="value mono">{formatUsd(call.costPerAnswerUsd)}</div>
        </div>
        <div className="field">
          <label>{t("colPipeline")}</label>
          <div className="value">{pipelineLabel(call.pipeline)}</div>
        </div>
        <div className="field">
          <label>{t("agentModel")}</label>
          <div className="value mono">{call.agentModel || "—"}</div>
        </div>
        <div className="field">
          <label>{t("totalTokens")}</label>
          <div className="value mono">{formatTokens(usage.totalTokens)}</div>
        </div>
      </div>
      <label
        style={{
          display: "block",
          marginTop: "var(--space-4)",
          fontSize: 11,
          color: "var(--color-muted)",
          textTransform: "uppercase",
          fontWeight: 600,
        }}
      >
        {t("tokenBreakdown")}
      </label>
      <div style={{ marginTop: "var(--space-3)", display: "grid", gap: "var(--space-2)" }}>
        {tokenRows.map(([label, tokens]) => (
          <div key={label} style={{ display: "flex", justifyContent: "space-between", gap: "var(--space-3)" }}>
            <span style={{ color: "var(--color-muted)" }}>{label}</span>
            <span className="mono">{formatTokens(tokens)}</span>
          </div>
        ))}
      </div>
    </Card>
  );
}

export function CallDetailPage() {
  const { token } = useAuth();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();
  const state = useApiData(() => getCallDetail(token!, Number(id)), [token, id]);

  return (
    <AppShell {...shell}>
      <div className="content narrow">
        {state.status === "success" && (
          <>
            <div className="breadcrumb">
              {t("callLog")} / {formatDayMonthTime(state.data.call.startedAt, language)}
            </div>
            <h1
              className="page-title"
              style={{ display: "flex", alignItems: "center", gap: "var(--space-3)", marginBottom: "var(--space-4)" }}
            >
              {formatDayMonthTime(state.data.call.startedAt, language)}{" "}
              <Pill variant={outcomePillVariant(state.data.call.outcome)}>
                {translateEnum(callOutcomeLabels, state.data.call.outcome, language)}
              </Pill>
            </h1>
            <Card>
              <div className="field-grid">
                <div className="field">
                  <label>{t("colClassification")}</label>
                  <div className="value">{translateEnum(callClassificationLabels, state.data.call.classification, language)}</div>
                </div>
                <div className="field">
                  <label>{t("colOutcome")}</label>
                  <div className="value">{translateEnum(callOutcomeLabels, state.data.call.outcome, language)}</div>
                </div>
                <div className="field">
                  <label>{t("colDurationShort")}</label>
                  <div className="value mono">{formatDuration(state.data.call.durationSeconds)}</div>
                </div>
                <div className="field">
                  <label>{t("callerQuestions")}</label>
                  <div className="value mono">{state.data.call.callerTurnCount}</div>
                </div>
                <div className="field">
                  <label>{t("turnsToResolution")}</label>
                  <div className="value mono">{state.data.call.turnCount}</div>
                </div>
                <div className="field">
                  <label>{t("colClient")}</label>
                  <div className="value">{state.data.call.clientName ?? state.data.call.callerPhoneNumber}</div>
                </div>
              </div>
            </Card>
            <CallCostCard call={state.data.call} />
            <Card>
              <label style={{ fontSize: 11, color: "var(--color-muted)", textTransform: "uppercase", fontWeight: 600 }}>
                {t("recording")}
              </label>
              <div style={{ marginTop: "var(--space-3)" }}>
                <AudioPlayer src={state.data.call.recordingUrl} />
              </div>
            </Card>
            <div className="actions">
              <Button variant="secondary" onClick={() => navigate("/appointments/calls")}>
                {t("backToCallLog")}
              </Button>
            </div>
          </>
        )}
        {state.status === "error" && <div className="field-error">{t("failedToLoadCallDetail")}</div>}
      </div>
    </AppShell>
  );
}
