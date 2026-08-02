import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { useAuth } from "@/shared/auth/AuthContext";
import { acceptEscalation, endEscalationCall, getRingingEscalations } from "@/shared/api/escalations";
import type { EscalationResponse } from "@/shared/api/types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

const HUB_URL = `${import.meta.env.VITE_API_BASE_URL as string}/hubs/escalations`;

/** Global overlay, mounted once for Owner/Staff — pushes the Flow D "ringing" alert over
 * SignalR rather than polling. Only one escalation is shown at a time; if more than one is
 * ringing, the rest wait their turn (per handoff.md's note) instead of stacking toasts. */
export function EscalationOverlay() {
  const { token, role } = useAuth();
  const { t } = useLanguage();
  const [queue, setQueue] = useState<EscalationResponse[]>([]);
  const [now, setNow] = useState(Date.now());
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const current = queue[0];

  useEffect(() => {
    if (!token || (role !== "Owner" && role !== "Staff")) return;

    getRingingEscalations(token)
      .then((ringing) => setQueue(ringing))
      .catch(() => {
        /* transient — the SignalR push will still surface new escalations */
      });

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    connection.on("escalationRinging", (escalation: EscalationResponse) => {
      setQueue((q) => [...q, escalation]);
    });
    connection.on("escalationConnected", (escalation: EscalationResponse) => {
      setQueue((q) => q.map((e) => (e.id === escalation.id ? escalation : e)));
    });
    connection.on("escalationAbandoned", (escalation: EscalationResponse) => {
      setQueue((q) => q.map((e) => (e.id === escalation.id ? escalation : e)));
      setTimeout(() => setQueue((q) => q.filter((e) => e.id !== escalation.id)), 4000);
    });
    connection.on("escalationEnded", (escalation: EscalationResponse) => {
      setQueue((q) => q.filter((e) => e.id !== escalation.id));
    });

    connection.start().catch(() => {
      /* the overlay simply won't receive live pushes until reconnected; ringing list on
       * mount plus withAutomaticReconnect covers most of this without extra UI noise */
    });
    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, [token, role]);

  useEffect(() => {
    if (!current || current.status !== "Connected") return;
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(interval);
  }, [current]);

  if (!current || !token) return null;

  async function handleAccept() {
    if (!token || !current) return;
    const updated = await acceptEscalation(token, current.id);
    setQueue((q) => q.map((e) => (e.id === updated.id ? updated : e)));
  }

  async function handleDismiss() {
    setQueue((q) => q.slice(1));
  }

  async function handleEnd() {
    if (!token || !current) return;
    await endEscalationCall(token, current.id);
    setQueue((q) => q.filter((e) => e.id !== current.id));
  }

  if (current.status === "Ringing") {
    return (
      <div className="escalation-toast ringing">
        <div className="kind">
          <span className="pulse-dot" />
          {t("incomingEscalatedCall")}
        </div>
        <div className="title">
          {t("callerLabel")} {current.callerPhoneNumber}
        </div>
        <div className="sub">
          {t("reasonLabel")} {current.reason}
        </div>
        <div className="actions">
          <button className="btn" onClick={handleAccept}>
            {t("acceptAndConnectMic")}
          </button>
          <button className="btn secondary" onClick={handleDismiss}>
            {t("dismiss")}
          </button>
        </div>
      </div>
    );
  }

  if (current.status === "Connected") {
    const acceptedAt = current.acceptedAt ? new Date(current.acceptedAt).getTime() : now;
    const elapsedSeconds = Math.max(0, Math.floor((now - acceptedAt) / 1000));
    const mm = String(Math.floor(elapsedSeconds / 60)).padStart(2, "0");
    const ss = String(elapsedSeconds % 60).padStart(2, "0");

    return (
      <div className="escalation-toast connected">
        <div className="kind">
          <span className="pulse-dot" />
          {t("liveConnected")}
        </div>
        <div className="title">
          {t("callerLabel")} {current.callerPhoneNumber}
        </div>
        <div className="sub timer">
          {t("callTimeLabel")} {mm}:{ss}
        </div>
        <div className="actions">
          <button className="btn danger" style={{ width: "100%" }} onClick={handleEnd}>
            {t("endCall")}
          </button>
        </div>
      </div>
    );
  }

  // Abandoned — informational only, auto-dismisses.
  return (
    <div className="escalation-toast">
      <div className="kind">{t("missed")}</div>
      <div className="title">
        {t("callerLabel")} {current.callerPhoneNumber}
      </div>
      <div className="sub">{t("noOneAcceptedInTime")}</div>
    </div>
  );
}
