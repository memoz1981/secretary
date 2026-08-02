import { Navigate } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "@/shared/auth/AuthContext";
import type { AccountRole } from "@/shared/api/types";

/** UX convenience only — hides navigation to pages a role shouldn't see. The backend
 * rejects unauthorized requests regardless of what this shows; never treat this as the
 * actual security boundary. */
export function RequireRole({ roles, children }: { roles: AccountRole[]; children: ReactNode }) {
  const { token, role } = useAuth();
  if (!token || !role) return <Navigate to="/login" replace />;
  if (!roles.includes(role)) return <Navigate to="/login" replace />;
  return <>{children}</>;
}
