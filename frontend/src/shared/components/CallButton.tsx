export function PhoneIcon({ size = 34 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M6.6 10.8c1.4 2.8 3.8 5.1 6.6 6.6l2.2-2.2c.3-.3.7-.4 1-.2 1.2.4 2.4.6 3.6.6.6 0 1 .4 1 1V20c0 .6-.4 1-1 1-9.4 0-17-7.6-17-17 0-.6.4-1 1-1h3.5c.6 0 1 .4 1 1 0 1.3.2 2.5.6 3.6.1.4 0 .8-.2 1l-2.3 2.2z"
        fill="currentColor"
      />
    </svg>
  );
}

/** The one round call button, wherever a call is placed from.
 *
 * Shared rather than copied because the demo Call page and the survey form both dial, and two
 * buttons that do the same thing but look slightly different is how an app starts feeling
 * assembled from parts. Green to dial, red and rotated to hang up, grey while connecting — the
 * state is in the shape and the colour, not only in the label beside it. */
export function CallButton({
  inCall,
  connecting,
  onDial,
  onHangUp,
  dialLabel,
  hangUpLabel,
  disabled = false,
}: {
  inCall: boolean;
  connecting: boolean;
  onDial: () => void;
  onHangUp: () => void;
  dialLabel: string;
  hangUpLabel: string;
  disabled?: boolean;
}) {
  const active = inCall || connecting;

  return (
    <button
      type="button"
      onClick={active ? onHangUp : onDial}
      disabled={connecting || disabled}
      aria-label={active ? hangUpLabel : dialLabel}
      style={{
        width: 92,
        height: 92,
        borderRadius: "50%",
        border: "none",
        cursor: connecting ? "wait" : disabled ? "not-allowed" : "pointer",
        color: "white",
        background: inCall ? "#d92d20" : connecting ? "#98a2b3" : disabled ? "#98a2b3" : "#12b76a",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        boxShadow: "0 6px 18px rgba(16, 24, 40, 0.2)",
        transform: inCall ? "rotate(135deg)" : "none",
        transition: "background 0.2s, transform 0.2s",
      }}
    >
      <PhoneIcon size={34} />
    </button>
  );
}
