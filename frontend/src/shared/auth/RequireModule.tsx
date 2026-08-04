import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "@/shared/auth/AuthContext";
import type { Module } from "@/shared/api/types";

/** Keeps a tenant out of a module they were not granted.
 *
 * The API already refuses — every module endpoint returns 403 — so nothing can be read or
 * written either way. What this stops is the shape of the failure: without it the page renders,
 * calls its endpoints, and shows a calendar that is empty because the request was rejected,
 * which reads as "no appointments" rather than "you do not have this".
 *
 * It also closes the way in. The nav is built from the URL, so reaching /appointments/calendar
 * by typing it or from a stale bookmark then produced a sidebar full of module links for a
 * tenant holding nothing. */
export function RequireModule({ module, children }: { module: Module; children: ReactNode }) {
  const { me } = useAuth();

  // /me has not landed yet. Rendering nothing beats redirecting somewhere we would immediately
  // leave once the answer arrives.
  if (!me) {
    return null;
  }

  if (!me.enabledModules.includes(module)) {
    return <Navigate to="/app" replace />;
  }

  return <>{children}</>;
}
