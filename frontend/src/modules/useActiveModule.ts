import { useLocation } from "react-router-dom";
import { useAuth } from "@/shared/auth/AuthContext";
import { MODULE_REGISTRY, usableModules, type ModuleManifest } from "@/modules/registry";

const REMEMBERED = "secretary.activeModule";

/** Which module the user is working in.
 *
 * The URL answers it inside a module, but not on the tenant-level pages — Clients, the Call Log,
 * the Dashboard and Admin belong to no module, and deriving purely from the path emptied the
 * sidebar the moment you opened one of them.
 *
 * So it is remembered. Entering a module records it; the shared pages keep showing that
 * module's links, which is also what makes them feel like part of where you were rather than a
 * dead end. A tenant with one module never notices any of this.
 *
 * sessionStorage, not localStorage: it should not outlive the tab, and it must never be the
 * reason a revoked module still looks available — every read is filtered by what /me currently
 * says the tenant holds. */
export function useActiveModule(): ModuleManifest | null {
  const { pathname } = useLocation();
  const { me } = useAuth();

  const held = usableModules(me?.enabledModules ?? []);
  if (held.length === 0) {
    return null;
  }

  const fromPath = MODULE_REGISTRY.find(
    (m) => pathname.startsWith(m.pathPrefix) && held.some((h) => h.key === m.key),
  );
  if (fromPath) {
    window.sessionStorage.setItem(REMEMBERED, fromPath.key);
    return fromPath;
  }

  const remembered = window.sessionStorage.getItem(REMEMBERED);
  return held.find((m) => m.key === remembered) ?? held[0];
}
