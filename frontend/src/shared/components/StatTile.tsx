/** One figure.
 *
 * ⚠ Clickable only when given somewhere to go, and it renders as a button only then. A tile that
 * looks pressable and does nothing is worse than a plain one: it teaches a reader that numbers on
 * this page do not open, and they stop trying the ones that do. */
export function StatTile({
  label,
  value,
  sub,
  onClick,
}: {
  label: string;
  value: string;
  sub?: string;
  onClick?: () => void;
}) {
  const body = (
    <>
      <label>{label}</label>
      <div className="value">{value}</div>
      {sub && <div className="sub">{sub}</div>}
    </>
  );

  return onClick === undefined ? (
    <div className="stat">{body}</div>
  ) : (
    <button type="button" className="stat clickable" onClick={onClick}>
      {body}
    </button>
  );
}
