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

function decode(token: string): DecodedToken {
  const payload = token.split(".")[1];
  return JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/"))) as DecodedToken;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [decoded, setDecoded] = useState<DecodedToken | null>(null);
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
  }

  function logout() {
    setToken(null);
    setDecoded(null);
    setMe(null);
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
