import { Route } from "react-router-dom";
import { RequireRole } from "@/shared/auth/RequireRole";
import { RequireModule } from "@/shared/auth/RequireModule";
import { CalendarPage } from "@/modules/appointments/pages/Calendar";
import { ServicesPage } from "@/modules/appointments/pages/Services";
import { ProvidersPage } from "@/modules/appointments/pages/Providers";
import type { ModuleManifest } from "@/modules/registry";

/** Randevu — the only module built. Everything it owns is declared here; nothing about it
 *  appears in App.tsx or in a shared navigation file. */
export const appointmentsModule: ModuleManifest = {
  key: "Appointment",
  pathPrefix: "/appointments",
  home: "/appointments/calendar",
  // The landing page's own copy, reused rather than duplicated: a tenant meeting the picker has
  // already read these words on the way in, and one set means the two cannot drift.
  titleKey: "moduleAppointmentsTitle",
  descriptionKey: "moduleAppointmentsText",

  nav: (_role, t) => [
    { label: t("navCalendar"), to: "/appointments/calendar" },
    { label: t("navServices"), to: "/appointments/services" },
    { label: t("navProviders"), to: "/appointments/providers" },
  ],

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
    </Route>
  ),
};
