import { Fragment } from "react";
import { useNavigate } from "react-router-dom";

export interface Crumb {
  label: string;
  /** Where this segment goes. Omit for the page you are on — the last one, normally. */
  to?: string;
}

/** Where you are, and the way back up.
 *
 * ⚠ A component rather than the markup written out on each page, because it was written out on
 * each page and they disagreed. One rendered every segment as plain text, so the trail said where
 * you were and offered no way back; the next made the linked segments buttons, which inherited
 * the browser's grey chrome and read as two controls followed by a label rather than as one
 * trail. Both were reported as bugs, separately.
 *
 * Every segment looks identical here. The ones that go somewhere reveal themselves on hover,
 * which is enough for a line that always sits in the same place on every page. */
export function Breadcrumbs({ items }: { items: Crumb[] }) {
  const navigate = useNavigate();

  return (
    <div className="breadcrumb">
      {items.map((item, index) => (
        <Fragment key={`${item.label}-${index}`}>
          {index > 0 && <span className="crumb-sep">/</span>}
          {item.to === undefined ? (
            <span>{item.label}</span>
          ) : (
            <button className="link" onClick={() => navigate(item.to!)}>
              {item.label}
            </button>
          )}
        </Fragment>
      ))}
    </div>
  );
}
