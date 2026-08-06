import { Route } from "react-router-dom";
import { RequireRole } from "@/shared/auth/RequireRole";
import { RequireModule } from "@/shared/auth/RequireModule";
import { CalendarPage } from "@/modules/appointments/pages/Calendar";
import { ServicesPage } from "@/modules/appointments/pages/Services";
import { ProvidersPage } from "@/modules/appointments/pages/Providers";
import { ClientsPage } from "@/modules/appointments/pages/Clients";
import { CallPage } from "@/modules/appointments/pages/Call";
import { CallLogPage } from "@/modules/appointments/pages/CallLog";
import { CallDetailPage } from "@/modules/appointments/pages/CallDetail";
import { DashboardPage } from "@/modules/appointments/pages/Dashboard";
import { EscalationOverlay } from "@/modules/appointments/components/EscalationOverlay";
import type { ModuleManifest } from "@/modules/registry";

/** Randevu — the only module built. Everything it owns is declared here; nothing about it
 *  appears in App.tsx or in a shared navigation file.
 *
 *  Clients, the call log and the dashboard are part of that "everything". They were tenant-level
 *  at first, on the reasoning that a business has one customer list — but a caller who books a
 *  haircut and a caller who asks the price of one are not the same record, and merging them
 *  would have made the Information module's callers appear in this module's client list. They
 *  are separate tables now (app.Clients, later inf.Clients) and separate pages to match. */
export const appointmentsModule: ModuleManifest = {
  key: "Appointment",
  pathPrefix: "/appointments",
  home: "/appointments/calendar",
  // The landing page's own copy, reused rather than duplicated: a tenant meeting the picker has
  // already read these words on the way in, and one set means the two cannot drift.
  titleKey: "moduleAppointmentsTitle",
  descriptionKey: "moduleAppointmentsText",

  nav: (role, t) => [
    { label: t("navCalendar"), to: "/appointments/calendar" },
    { label: t("navServices"), to: "/appointments/services" },
    { label: t("navProviders"), to: "/appointments/providers" },
    { label: t("navClients"), to: "/appointments/clients" },
    // Demo live call to the AI agent — Owner only, matching the /voice/live-call policy.
    ...(role === "Owner" ? [{ label: t("navCall"), to: "/appointments/call" }] : []),
    { label: t("navCallLog"), to: "/appointments/calls" },
    { label: t("navDashboard"), to: "/appointments/dashboard" },
  ],

  // The escalation overlay listens on this module's SignalR hub, so it is mounted only for
  // tenants holding the module — otherwise a tenant without it opens a connection the API now
  // refuses.
  overlay: <EscalationOverlay />,

  // Role says who you are, module says what your tenant bought. Both guard every page, the same
  // pairing the API applies.
  routes: (
    <Route path="/appointments">
      <Route
        path="calendar"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Appointment">
              <CalendarPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="services"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Appointment">
              <ServicesPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="providers"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Appointment">
              <ProvidersPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="clients"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Appointment">
              <ClientsPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="call"
        element={
          <RequireRole roles={["Owner"]}>
            <RequireModule module="Appointment">
              <CallPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="calls"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Appointment">
              <CallLogPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="calls/:id"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Appointment">
              <CallDetailPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="dashboard"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Appointment">
              <DashboardPage />
            </RequireModule>
          </RequireRole>
        }
      />
    </Route>
  ),
};
