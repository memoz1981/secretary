import { useEffect, useState, type ReactNode } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@/shared/auth/AuthContext";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { ThemeSelect } from "@/shared/components/ThemeSelect";
import { LanguageSwitch } from "@/shared/components/LanguageSwitch";

export interface NavItem {
  label: string;
  to: string;
}

interface AppShellProps {
  brand: string;
  domainLabel: string;
  navItems: NavItem[];
  whoText: string;
  children: ReactNode;
}

export function AppShell({ brand, domainLabel, navItems, whoText, children }: AppShellProps) {
  const { t } = useLanguage();
  const { logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  // Mobile-only (≤720px, see components.css): the sidebar is off-canvas and this
  // controls whether it's slid in. On desktop the class has no effect.
  const [menuOpen, setMenuOpen] = useState(false);

  // Navigating always closes the drawer — covers nav links, breadcrumbs, and
  // programmatic redirects alike.
  useEffect(() => {
    setMenuOpen(false);
  }, [location.pathname]);

  function handleSignOut() {
    logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className="app-shell">
      {menuOpen && <div className="sidebar-backdrop" onClick={() => setMenuOpen(false)} />}
      <div className={menuOpen ? "sidebar open" : "sidebar"}>
        <div className="brand">{brand}</div>
        <div className="domain-label">{domainLabel}</div>
        <nav>
          {navItems.map((item) => (
            <NavLink key={item.to} to={item.to} className={({ isActive }) => (isActive ? "active" : "")}>
              {item.label}
            </NavLink>
          ))}
        </nav>
      </div>
      <div className="main">
        <div className="topbar">
          <button
            type="button"
            className="menu-btn"
            aria-label="Menu"
            aria-expanded={menuOpen}
            onClick={() => setMenuOpen((open) => !open)}
          >
            <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
              <path d="M2 4h14M2 9h14M2 14h14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </button>
          <div className="who">{whoText}</div>
          <ThemeSelect className="theme-select" />
          <LanguageSwitch />
          <button
            type="button"
            className="sign-out"
            onClick={handleSignOut}
            style={{
              background: "none",
              border: "1px solid currentColor",
              borderRadius: 6,
              cursor: "pointer",
              font: "inherit",
              color: "inherit",
              padding: "2px 10px",
              marginLeft: "var(--space-3, 12px)",
            }}
          >
            {t("signOut")}
          </button>
        </div>
        <div className="content">{children}</div>
      </div>
    </div>
  );
}
