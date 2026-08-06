import { Route } from "react-router-dom";
import { RequireRole } from "@/shared/auth/RequireRole";
import { RequireModule } from "@/shared/auth/RequireModule";
import { InformationOverviewPage } from "@/modules/information/pages/Overview";
import type { ModuleManifest } from "@/modules/registry";

/** Məlumat xətti — a second module with one placeholder page behind it.
 *
 * Its real purpose today is to make the multi-module paths reachable. The picker only renders
 * for a tenant holding two modules and the sidebar switcher appears on the same condition, so
 * until something could be the second one, both were written and never seen.
 *
 * It is also the first honest test of the registry's claim: adding a module should be a folder
 * and one line, with no edit to App.tsx and no edit to a shared navigation file. It was. */
export const informationModule: ModuleManifest = {
  key: "Information",
  pathPrefix: "/information",
  home: "/information/overview",
  titleKey: "moduleInfoTitle",
  descriptionKey: "moduleInfoText",

  nav: (_role, t) => [{ label: t("navInfoOverview"), to: "/information/overview" }],

  routes: (
    <Route path="/information">
      <Route
        path="overview"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Information">
              <InformationOverviewPage />
            </RequireModule>
          </RequireRole>
        }
      />
    </Route>
  ),
};
