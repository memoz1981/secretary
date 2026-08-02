import { createContext, useContext, useState, type ReactNode } from "react";
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

  function login(newToken: string) {
    setToken(newToken);
    setDecoded(decode(newToken));
    getMe(newToken)
      .then(setMe)
      .catch(() => {
        /* non-fatal — the sidebar/topbar just fall back to showing the role */
      });
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
