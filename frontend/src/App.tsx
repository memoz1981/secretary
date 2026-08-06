import { Navigate, Route, Routes, useParams } from "react-router-dom";
import { AuthProvider } from "@/shared/auth/AuthContext";
import { RequireRole } from "@/shared/auth/RequireRole";
import { LandingPage } from "@/pages/Landing";
import { LoginPage } from "@/pages/Login";
import { AppEntryPage } from "@/pages/AppEntry";
import { TenantListPage } from "@/pages/TenantList";
import { CreateTenantPage } from "@/pages/CreateTenant";
import { TenantDetailPage } from "@/pages/TenantDetail";
import { AdminPage } from "@/pages/Admin";
import { MODULE_REGISTRY } from "@/modules/registry";
import { ModuleOverlays } from "@/modules/ModuleOverlays";

/** The one old URL that carries something worth keeping — a bookmarked call should land on that
 *  call, not on the top of the list. */
function RedirectToCallDetail() {
  const { id } = useParams();
  return <Navigate to={`/appointments/calls/${id}`} replace />;
}

export function App() {
  return (
    <AuthProvider>
      <ModuleOverlays />
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/login" element={<LoginPage />} />

        {/* The single place that decides where a signed-in user starts. Login always comes
            here, never straight to a module, so refresh and deep links behave the same. */}
        <Route path="/app" element={<AppEntryPage />} />

        <Route
          path="/admin/tenants"
          element={
            <RequireRole roles={["PlatformAdmin"]}>
              <TenantListPage />
            </RequireRole>
          }
        />
        <Route
          path="/admin/tenants/new"
          element={
            <RequireRole roles={["PlatformAdmin"]}>
              <CreateTenantPage />
            </RequireRole>
          }
        />
        <Route
          path="/admin/tenants/:id"
          element={
            <RequireRole roles={["PlatformAdmin"]}>
              <TenantDetailPage />
            </RequireRole>
          }
        />

        {/* Each module contributes its own routes under its own prefix. Adding a module is a
            folder and one line in the registry — nothing changes in this file. */}
        {MODULE_REGISTRY.map((module) => module.routes)}

        {/* Tenant-level, owned by no module and the only such page left: one set of business
            details and one list of staff accounts, however many modules the tenant holds.
            Everything else that used to live here turned out to belong to a module — see the
            appointments manifest. */}
        <Route
          path="/admin"
          element={
            <RequireRole roles={["Owner"]}>
              <AdminPage />
            </RequireRole>
          }
        />

        {/* The pre-module URLs. Redirects rather than deletions: they are in browser history
            and bookmarks, and landing a stale bookmark on the front door would read as "logged
            out" rather than "this moved". */}
        <Route path="/calendar" element={<Navigate to="/appointments/calendar" replace />} />
        <Route path="/services" element={<Navigate to="/appointments/services" replace />} />
        <Route path="/providers" element={<Navigate to="/appointments/providers" replace />} />
        <Route path="/clients" element={<Navigate to="/appointments/clients" replace />} />
        <Route path="/call" element={<Navigate to="/appointments/call" replace />} />
        <Route path="/calls" element={<Navigate to="/appointments/calls" replace />} />
        <Route path="/calls/:id" element={<RedirectToCallDetail />} />
        <Route path="/dashboard" element={<Navigate to="/appointments/dashboard" replace />} />

        {/* An unknown URL lands on the front door, not on a login form — a stray link is far
            more likely to be a visitor than a locked-out customer. */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthProvider>
  );
}
