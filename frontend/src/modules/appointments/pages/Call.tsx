import { useEffect, useRef, useState } from "react";
import { AppShell } from "@/shared/components/AppShell";
import { useAuth } from "@/shared/auth/AuthContext";
import { useBusinessShell } from "@/shared/lib/appShellProps";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { LiveVoiceCall, type LiveCallStatus } from "@/shared/lib/liveVoiceCall";
import { CALL_PIPELINES, type CallPipeline } from "@/shared/api/types";
import { PIPELINE_INFO } from "@/shared/lib/pipelines";
import { Card } from "@/shared/components/Card";
import { AgentAvatar } from "@/shared/components/AgentAvatar";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

type PageStatus = "idle" | LiveCallStatus;

function PhoneIcon({ size = 34 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M6.62 10.79a15.05 15.05 0 0 0 6.59 6.59l2.2-2.2a1 1 0 0 1 1.02-.24c1.12.37 2.33.57 3.57.57a1 1 0 0 1 1 1V20a1 1 0 0 1-1 1C10.85 21 3 13.15 3 3.5a1 1 0 0 1 1-1H7.5a1 1 0 0 1 1 1c0 1.24.2 2.45.57 3.57a1 1 0 0 1-.25 1.02l-2.2 2.2Z" />
    </svg>
  );
}

export function CallPage() {
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
      // This page belongs to the Appointment module, so it dials the appointment line. An
      // Orders module gets its own Call page rather than a picker here — a real caller does not
      // choose which business they reached.
      "Appointment",
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

          <button
            type="button"
            onClick={inCall || status === "connecting" ? endCall : startCall}
            disabled={status === "connecting"}
            aria-label={inCall ? t("callHangUp") : t("callDial")}
            style={{
              width: 76,
              height: 76,
              borderRadius: "50%",
              border: "none",
              cursor: status === "connecting" ? "wait" : "pointer",
              color: "white",
              background: inCall ? "#d92d20" : status === "connecting" ? "#98a2b3" : "#12b76a",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              boxShadow: "0 6px 18px rgba(16, 24, 40, 0.2)",
              transform: inCall ? "rotate(135deg)" : "none",
              transition: "background 0.2s, transform 0.2s",
            }}
          >
            <PhoneIcon size={28} />
          </button>

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
