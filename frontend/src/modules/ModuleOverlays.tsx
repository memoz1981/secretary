import { Fragment } from "react";
import { useAuth } from "@/shared/auth/AuthContext";
import { usableModules } from "@/modules/registry";

/** Mounts each held module's overlay, and only those.
 *
 * The escalation toast used to be mounted globally in App. That was fine while there was one
 * module, and wrong the moment there were two: it opened a SignalR connection to the
 * appointment hub for every signed-in user, including tenants who do not have the module and
 * whom the hub now rejects.
 *
 * Keeping the decision here rather than inside the overlay means a module still cannot leak
 * into App.tsx — the registry stays the only list. */
export function ModuleOverlays() {
  const { me } = useAuth();

  if (!me) {
    return null;
  }

  return (
    <>
      {usableModules(me.enabledModules)
        .filter((m) => m.overlay)
        .map((m) => (
          <Fragment key={m.key}>{m.overlay}</Fragment>
        ))}
    </>
  );
}
