import { Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider } from "@/shared/auth/AuthContext";
import { RequireRole } from "@/shared/auth/RequireRole";
import { EscalationOverlay } from "@/shared/components/EscalationOverlay";
import { LandingPage } from "@/pages/Landing";
import { LoginPage } from "@/pages/Login";
import { TenantListPage } from "@/pages/TenantList";
import { CreateTenantPage } from "@/pages/CreateTenant";
import { TenantDetailPage } from "@/pages/TenantDetail";
import { CalendarPage } from "@/modules/appointments/pages/Calendar";
import { ServicesPage } from "@/modules/appointments/pages/Services";
import { ProvidersPage } from "@/modules/appointments/pages/Providers";
import { ClientsPage } from "@/pages/Clients";
import { AdminPage } from "@/pages/Admin";
import { CallLogPage } from "@/pages/CallLog";
import { CallPage } from "@/pages/Call";
import { CallDetailPage } from "@/pages/CallDetail";
import { DashboardPage } from "@/pages/Dashboard";

export function App() {
  return (
    <AuthProvider>
      <EscalationOverlay />
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/login" element={<LoginPage />} />

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

        <Route
          path="/calendar"
          element={
            <RequireRole roles={["Owner", "Staff"]}>
              <CalendarPage />
            </RequireRole>
          }
        />
        <Route
          path="/services"
          element={
            <RequireRole roles={["Owner", "Staff"]}>
              <ServicesPage />
            </RequireRole>
          }
        />
        <Route
          path="/providers"
          element={
            <RequireRole roles={["Owner", "Staff"]}>
              <ProvidersPage />
            </RequireRole>
          }
        />
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

        {/* An unknown URL lands on the front door, not on a login form — a stray link is far
            more likely to be a visitor than a locked-out customer. */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthProvider>
  );
}
