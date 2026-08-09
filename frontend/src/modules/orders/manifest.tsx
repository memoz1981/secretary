import { Route } from "react-router-dom";
import { RequireRole } from "@/shared/auth/RequireRole";
import { RequireModule } from "@/shared/auth/RequireModule";
import { LiveCallPage } from "@/shared/components/LiveCallPage";
import { ProductsPage } from "@/modules/orders/pages/Products";
import { OrdersPage } from "@/modules/orders/pages/Orders";
import { OrderCustomersPage } from "@/modules/orders/pages/Customers";
import type { ModuleManifest } from "@/modules/registry";

/** Sifariş qəbulu — the order line.
 *
 * Products is the page that has to exist before anything else works: an empty catalogue means
 * the agent can only tell callers it sells nothing. Orders and Customers are read-only, because
 * every row in them was created by a phone call and a form that could invent one would be a
 * form that disagreed with the recording. */
export const ordersModule: ModuleManifest = {
  key: "Order",
  pathPrefix: "/orders",
  home: "/orders/products",
  titleKey: "moduleOrdersTitle",
  descriptionKey: "moduleOrdersText",

  nav: (role, t) => [
    { label: t("products"), to: "/orders/products" },
    { label: t("orders"), to: "/orders/list" },
    { label: t("navOrderCustomers"), to: "/orders/customers" },
    ...(role === "Owner" ? [{ label: t("navCall"), to: "/orders/call" }] : []),
  ],

  routes: (
    <Route path="/orders">
      <Route
        path="products"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Order">
              <ProductsPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="list"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Order">
              <OrdersPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="customers"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Order">
              <OrderCustomersPage />
            </RequireModule>
          </RequireRole>
        }
      />
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
