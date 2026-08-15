import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useLocation } from "react-router-dom";
import type { AccountRole, MeResponse } from "@/shared/api/types";
import { getMe } from "@/shared/api/auth";

// Matches the backend's actual JWT claim keys (see AiAppointment.Api.Auth.AppClaimTypes):
// "sub" (standard), "role" (short key, not the ClaimTypes.Role URI), "tenant_id" (custom).
interface DecodedToken {
  sub: string;
  role: AccountRole;
  tenant_id?: string;
  exp: number;
}

interface AuthContextValue {
  token: string | null;
  accountId: string | null;
  role: AccountRole | null;
  tenantId: string | null;
  /** The caller's real name/email and (if tenant-scoped) tenant business name — the JWT
   * deliberately carries none of this, so it's fetched once from GET /api/auth/me right
   * after login rather than embedded in the token. Null until that fetch resolves. */
  me: MeResponse | null;
  login: (token: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

const STORAGE_KEY = "sekretar.token";

function decode(token: string): DecodedToken {
  const payload = token.split(".")[1];
  return JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/"))) as DecodedToken;
}

/** The session, restored from the last visit — or null, which means the login page.
 *
 * The token used to live only in React state, so every refresh, every new tab and every time
 * somebody typed the address again started from null and bounced them back to login. The 8-hour
 * expiry the server issues was meaningless: the session died on the first F5.
 *
 * ⚠ Every access is wrapped. Reading localStorage unguarded during the first render is what
 * takes the whole app down when a browser blocks site data — the provider wraps the router, so
 * the throw is a white screen rather than a degraded page. See the theme bootstrap in index.html,
 * which learned this first.
 *
 * An expired token is discarded here rather than restored. Handing it back would produce a
 * session that looks logged in and 401s on every request — worse than the login page, because
 * there is nothing on screen to explain it.
 */
function readStoredSession(): { token: string; decoded: DecodedToken } | null {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;

    const decoded = decode(stored);
    if (typeof decoded.exp !== "number" || decoded.exp * 1000 <= Date.now()) {
      window.localStorage.removeItem(STORAGE_KEY);
      return null;
    }

    return { token: stored, decoded };
  } catch {
    // Unreadable storage, or a token that is not a JWT any more. Either way: log in again.
    return null;
  }
}

function writeStoredToken(token: string | null) {
  try {
    if (token) {
      window.localStorage.setItem(STORAGE_KEY, token);
    } else {
      window.localStorage.removeItem(STORAGE_KEY);
    }
  } catch {
    // Storage blocked. The session still works for this tab, it just will not survive a reload.
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  // Read once, lazily, before the first paint — so a reload never flashes the login page at
  // somebody who is already signed in.
  const [restored] = useState(readStoredSession);
  const [token, setToken] = useState<string | null>(restored?.token ?? null);
  const [decoded, setDecoded] = useState<DecodedToken | null>(restored?.decoded ?? null);
  const [me, setMe] = useState<MeResponse | null>(null);
  const { pathname } = useLocation();

  // Re-read on every navigation, not just at login.
  //
  // /me carries enabledModules, and the guards route on it. Fetched once, a tenant who was in
  // the app when an admin revoked a module kept the old answer for the rest of their session:
  // the sidebar still offered the module and its pages still opened. The API refused every
  // request underneath, so nothing leaked — but the app disagreed with the server about what
  // the tenant had, which is exactly the confusion the per-request lookup on the server side
  // exists to avoid.
  //
  // One small request per navigation is a fair price for the client and the server agreeing.
  useEffect(() => {
    if (!token) return;
    getMe(token)
      .then(setMe)
      .catch(() => {
        /* non-fatal — the shell falls back to showing the role */
      });
  }, [token, pathname]);

  function login(newToken: string) {
    setToken(newToken);
    setDecoded(decode(newToken));
    writeStoredToken(newToken);
  }

  function logout() {
    setToken(null);
    setDecoded(null);
    setMe(null);
    writeStoredToken(null);
  }

  return (
    <AuthContext.Provider
      value={{
        token,
        accountId: decoded?.sub ?? null,
        role: decoded?.role ?? null,
        tenantId: decoded?.tenant_id ?? null,
        me,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
