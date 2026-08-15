import { useEffect, useRef, useState } from "react";
import { CallButton } from "@/shared/components/CallButton";
import { AppShell } from "@/shared/components/AppShell";
import { useAuth } from "@/shared/auth/AuthContext";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { LiveVoiceCall, type LiveCallStatus } from "@/shared/lib/liveVoiceCall";
import { CALL_PIPELINES, type CallPipeline, type Module } from "@/shared/api/types";
import { PIPELINE_INFO } from "@/shared/lib/pipelines";
import { Card } from "@/shared/components/Card";
import { AgentAvatar } from "@/shared/components/AgentAvatar";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

type PageStatus = "idle" | LiveCallStatus;


/** The demo call page: a microphone, a pipeline picker, and a line to dial.
 *
 * Shared plumbing rather than a module's own page — the mic and the pipeline comparison are
 * identical whoever answers. What differs is only which line it rings, so each module owns the
 * route and passes its own module here. A real caller never chooses; they dial a number. */
export function LiveCallPage({ module }: { module: Module }) {
  const { token } = useAuth();
  const { language, t } = useLanguage();
  const shell = useBusinessShell();
  const [status, setStatus] = useState<PageStatus>("idle");
  const [micDenied, setMicDenied] = useState(false);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [pipeline, setPipeline] = useState<CallPipeline>("OpenAiRealtime_2_1");
  const callRef = useRef<LiveVoiceCall | null>(null);

  const inCall = status === "in-call";

  useEffect(() => {
    if (!inCall) return;
    setElapsedSeconds(0);
    const timer = window.setInterval(() => setElapsedSeconds((s) => s + 1), 1000);
    return () => window.clearInterval(timer);
  }, [inCall]);

  // Leaving the page mid-call must release the microphone, not keep streaming in the background.
  useEffect(() => {
    return () => {
      void callRef.current?.hangUp();
    };
  }, []);

  async function startCall() {
    if (!token) return;
    setMicDenied(false);
    const call = new LiveVoiceCall(
      API_BASE_URL,
      token,
      {
        onStatus: (next) => {
          setStatus(next);
          if (next === "ended" || next === "error") callRef.current = null;
        },
      },
      pipeline,
      module,
    );
    callRef.current = call;
    try {
      await call.start();
    } catch (error) {
      callRef.current = null;
      // Most common: the user denied microphone access.
      setMicDenied(error instanceof DOMException && error.name === "NotAllowedError");
      setStatus("error");
    }
  }

  async function endCall() {
    await callRef.current?.hangUp();
    callRef.current = null;
  }

  const statusText: Record<PageStatus, string> = {
    idle: t("callPressToDial"),
    connecting: t("callConnecting"),
    "in-call": `${t("callInProgress")} · ${Math.floor(elapsedSeconds / 60)}:${String(elapsedSeconds % 60).padStart(2, "0")}`,
    ended: t("callEnded"),
    error: micDenied ? t("callMicDenied") : t("callFailed"),
  };

  return (
    <AppShell {...shell}>
      {/* Identity left, controls right — a single band across the top rather than a centred
          column, so the legend below stays on the same screen without scrolling. */}
      <div className="call-layout">
        {/* Who you are calling. The agent has a name and a voice already; giving it a face
            makes this read as a person to reach rather than a feature to trigger. */}
        <div className="agent-card">
          <AgentAvatar />
          <h1 className="agent-name">Lamiya</h1>
          <div className="agent-role">{t("agentRole")}</div>
        </div>

        <div className="call-controls">
          {/* Which pipeline answers. Locked while a call is up — the sample rate is fixed when
              the audio context opens, so switching mid-call would send the wrong rate and the
              agent would still transcribe *something*, which is worse than failing outright. */}
          <div className="view-toggle" aria-label={t("callPipeline")}>
            {CALL_PIPELINES.map((option) => {
              // Locked while a call is up, and permanently for an option the server will
              // refuse — it still shows, so the roadmap is visible.
              const locked = status === "connecting" || inCall || !PIPELINE_INFO[option].enabled;
              return (
                <span
                  key={option}
                  className={[pipeline === option ? "active" : "", locked ? "disabled" : ""].filter(Boolean).join(" ")}
                  onClick={() => {
                    if (!locked) setPipeline(option);
                  }}
                >
                  {PIPELINE_INFO[option].short}
                </span>
              );
            })}
          </div>

          <CallButton
            inCall={inCall}
            connecting={status === "connecting"}
            onDial={startCall}
            onHangUp={endCall}
            dialLabel={t("callDial")}
            hangUpLabel={t("callHangUp")}
          />

          <div className="call-status">{statusText[status]}</div>
          {/* The instruction only earns its space before the call starts — once the line is
              open the status above it is the thing worth reading. */}
          {status === "idle" && <p className="call-hint">{t("callHint")}</p>}
        </div>
      </div>

      {/* The legend. Here rather than in a wiki because the trade-off has to be visible at the
          moment of choosing — these four differ by roughly tenfold in cost and several times in
          latency and language accuracy, and none of that is guessable from a toggle label. */}
      <Card>
        <h2>{t("callPipelineLegend")}</h2>
        <div className="table-scroll">
          <table className="data striped" style={{ marginTop: "var(--space-3)" }}>
            <thead>
              <tr>
                <th>{t("callPipeline")}</th>
                <th>{t("callPipelineWhat")}</th>
                <th>{t("callPipelineTradeOff")}</th>
                <th style={{ whiteSpace: "nowrap" }}>{t("costPerMinute")}</th>
              </tr>
            </thead>
            <tbody>
              {CALL_PIPELINES.map((option) => {
                const info = PIPELINE_INFO[option];
                return (
                  <tr key={option} className={pipeline === option ? "selected" : ""}>
                    <td style={{ whiteSpace: "nowrap" }}>{info.short}</td>
                    <td>{info.what[language]}</td>
                    <td>{info.tradeOff[language]}</td>
                    <td className="mono" style={{ whiteSpace: "nowrap" }}>
                      {info.costPerMinute}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <div className="note" style={{ marginTop: "var(--space-3)" }}>
          {t("callPipelineNote")}
        </div>
      </Card>
    </AppShell>
  );
}
