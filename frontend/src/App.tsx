import { Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider } from "@/shared/auth/AuthContext";
import { RequireRole } from "@/shared/auth/RequireRole";
import { EscalationOverlay } from "@/shared/components/EscalationOverlay";
import { LandingPage } from "@/pages/Landing";
import { LoginPage } from "@/pages/Login";
import { AppEntryPage } from "@/pages/AppEntry";
import { TenantListPage } from "@/pages/TenantList";
import { CreateTenantPage } from "@/pages/CreateTenant";
import { TenantDetailPage } from "@/pages/TenantDetail";
import { ClientsPage } from "@/pages/Clients";
import { AdminPage } from "@/pages/Admin";
import { CallLogPage } from "@/pages/CallLog";
import { CallPage } from "@/pages/Call";
import { CallDetailPage } from "@/pages/CallDetail";
import { DashboardPage } from "@/pages/Dashboard";
import { MODULE_REGISTRY } from "@/modules/registry";

export function App() {
  return (
    <AuthProvider>
      <EscalationOverlay />
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

        {/* Tenant-level, owned by no module: one customer list, one phone line, one bill and
            one set of business details, however many modules the tenant holds. */}
        <Route
          path="/clients"
          element={
            <RequireRole roles={["Owner", "Staff"]}>
              <ClientsPage />
            </RequireRole>
          }
        />
        <Route
          path="/admin"
          element={
            <RequireRole roles={["Owner"]}>
              <AdminPage />
            </RequireRole>
          }
        />
        <Route
          path="/call"
          element={
            <RequireRole roles={["Owner"]}>
              <CallPage />
            </RequireRole>
          }
        />
        <Route
          path="/calls"
          element={
            <RequireRole roles={["Owner", "Staff"]}>
              <CallLogPage />
            </RequireRole>
          }
        />
        <Route
          path="/calls/:id"
          element={
            <RequireRole roles={["Owner", "Staff"]}>
              <CallDetailPage />
            </RequireRole>
          }
        />
        <Route
          path="/dashboard"
          element={
            <RequireRole roles={["Owner", "Staff"]}>
              <DashboardPage />
            </RequireRole>
          }
        />

        {/* The pre-module URLs. Redirects rather than deletions: they are in browser history
            and bookmarks, and landing a stale bookmark on the front door would read as "logged
            out" rather than "this moved". */}
        <Route path="/calendar" element={<Navigate to="/appointments/calendar" replace />} />
        <Route path="/services" element={<Navigate to="/appointments/services" replace />} />
        <Route path="/providers" element={<Navigate to="/appointments/providers" replace />} />

        {/* An unknown URL lands on the front door, not on a login form — a stray link is far
            more likely to be a visitor than a locked-out customer. */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthProvider>
  );
}
