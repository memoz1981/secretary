import { Route } from "react-router-dom";
import { RequireRole } from "@/shared/auth/RequireRole";
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
  titleKey: "moduleAppointment",
  descriptionKey: "moduleAppointmentDescription",

  nav: (_role, t) => [
    { label: t("navCalendar"), to: "/appointments/calendar" },
    { label: t("navServices"), to: "/appointments/services" },
    { label: t("navProviders"), to: "/appointments/providers" },
  ],

  routes: (
    <Route path="/appointments">
      <Route
        path="calendar"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <CalendarPage />
          </RequireRole>
        }
      />
      <Route
        path="services"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <ServicesPage />
          </RequireRole>
        }
      />
      <Route
        path="providers"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <ProvidersPage />
          </RequireRole>
        }
      />
    </Route>
  ),
};
