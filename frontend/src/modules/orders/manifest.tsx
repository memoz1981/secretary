import { Route } from "react-router-dom";
import { RequireRole } from "@/shared/auth/RequireRole";
import { RequireModule } from "@/shared/auth/RequireModule";
import { LiveCallPage } from "@/shared/components/LiveCallPage";
import type { ModuleManifest } from "@/modules/registry";

/** Sifariş qəbulu — the order line.
 *
 * One page so far, and it is the one that matters: the call. The agent behind it is complete —
 * it identifies the caller, takes the order and books a delivery day — but nothing yet lets a
 * tenant manage products or read what was ordered. Those are the next thing, and until they
 * exist the catalogue has to be seeded by hand. */
export const ordersModule: ModuleManifest = {
  key: "Order",
  pathPrefix: "/orders",
  home: "/orders/call",
  titleKey: "moduleOrdersTitle",
  descriptionKey: "moduleOrdersText",

  nav: (role, t) => [
    ...(role === "Owner" ? [{ label: t("navCall"), to: "/orders/call" }] : []),
  ],

  routes: (
    <Route path="/orders">
      <Route
        path="call"
        element={
          <RequireRole roles={["Owner"]}>
            <RequireModule module="Order">
              <LiveCallPage module="Order" />
            </RequireModule>
          </RequireRole>
        }
      />
    </Route>
  ),
};
