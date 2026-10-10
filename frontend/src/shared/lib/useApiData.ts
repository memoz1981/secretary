import { useEffect, useState } from "react";

export type FetchState<T> =
  | { status: "loading" }
  | { status: "error"; error: unknown }
  | { status: "success"; data: T };

/** Fetch-on-mount/dependency-change with loading/error/success tracking — deliberately no
 * caching, retries, or background refetching. If a page's needs grow past this, that's the
 * signal to have the "add a data-fetching library?" conversation explicitly, not to quietly
 * build a worse version of one by hand. */
/** @param keepPrevious Hold the last successful data while a refetch is in flight, instead of
 *  dropping back to "loading".
 *
 *  ⚠ For a page that refetches in place after saving. Without it, every save emptied the page:
 *  the render is gated on the data, the data went null while the reload flew, the page collapsed
 *  to nothing and the browser threw the scroll position to the top — mid-sentence, on a form
 *  somebody had just used. Off by default, because a first load genuinely has nothing to show. */
export function useApiData<T>(
  fetcher: () => Promise<T>,
  deps: unknown[],
  { keepPrevious = false }: { keepPrevious?: boolean } = {},
): FetchState<T> {
  const [state, setState] = useState<FetchState<T>>({ status: "loading" });

  useEffect(() => {
    let cancelled = false;
    setState((current) =>
      keepPrevious && current.status === "success" ? current : { status: "loading" },
    );
    fetcher()
      .then((data) => {
        if (!cancelled) setState({ status: "success", data });
      })
      .catch((error) => {
        if (!cancelled) setState({ status: "error", error });
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return state;
}
