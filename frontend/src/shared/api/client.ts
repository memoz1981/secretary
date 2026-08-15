const BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

type RequestOptions = {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  body?: unknown;
  token?: string | null;
};

/** Everyone who wants to hear about a write the server refused. */
type FailureListener = (error: ApiError) => void;

const failureListeners = new Set<FailureListener>();

/**
 * Announces writes the server refused, so something on screen can say so.
 *
 * ⚠ This exists because the app used to fail in complete silence. A click handler would
 * `await removeProvider(...)` with nothing around it, the promise would reject, and the browser
 * would drop it — 26 mutation call sites across 12 files, 9 of those files with no catch at all.
 * The server's own sentence ("Provider '3' is linked to upcoming appointments and cannot be
 * removed.") travelled the whole way to the browser and was thrown away by the last three lines.
 *
 * Announcing from here rather than fixing 26 handlers is the point: a page cannot forget to
 * report an error it is not responsible for reporting. A handler that wants to present a failure
 * itself still catches, and does — this only guarantees the floor.
 *
 * GET is deliberately excluded. Reads are already reported by useApiData's error state, and
 * publishing them too would say everything twice.
 */
export function onApiFailure(listener: FailureListener): () => void {
  failureListeners.add(listener);
  return () => failureListeners.delete(listener);
}

export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = "GET", body, token } = options;

  const response = await fetch(`${BASE_URL}${path}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    const error = new ApiError(response.status, text || response.statusText);

    // 401 is not a refusal worth a banner — it means the session went, and the guards send the
    // caller to the login page, which explains itself.
    if (method !== "GET" && response.status !== 401) {
      failureListeners.forEach((listen) => listen(error));
    }

    throw error;
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Builds a query string from a plain object, skipping null/undefined values. */
export function toQueryString(params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined) {
      search.set(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}
