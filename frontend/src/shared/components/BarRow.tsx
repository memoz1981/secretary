export function BarRow({ label, pct, color }: { label: string; pct: number; color?: string }) {
  const clamped = Math.max(0, Math.min(100, Math.round(pct * 100)));
  return (
    <div className="bar-row">
      <div className="label">{label}</div>
      <div className="bar-track">
        <div className="bar-fill" style={{ width: `${clamped}%`, background: color }} />
      </div>
      <div className="pct">{clamped}%</div>
    </div>
  );
}
