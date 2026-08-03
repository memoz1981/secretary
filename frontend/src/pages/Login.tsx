import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { login as loginRequest } from "@/shared/api/auth";
import { ApiError } from "@/shared/api/client";
import { useAuth } from "@/shared/auth/AuthContext";
import { TextField } from "@/shared/components/FormControls";
import { Button } from "@/shared/components/Button";
import { ThemeSelect } from "@/shared/components/ThemeSelect";
import { LanguageSwitch } from "@/shared/components/LanguageSwitch";
import { Logo } from "@/shared/components/Logo";
import { isRequired } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const { t } = useLanguage();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (!isRequired(email) || !isRequired(password)) {
      setError(t("emailAndPasswordRequired"));
      return;
    }

    setSubmitting(true);
    try {
      const result = await loginRequest({ email, password });
      login(result.token);
      if (result.role === "PlatformAdmin") {
        navigate("/admin/tenants");
      } else {
        // /app decides: one module goes straight through, several show the picker. Login does
        // not need to know which — and putting that here would duplicate it for refresh and
        // deep links too.
        navigate("/app");
      }
    } catch (err) {
      setError(err instanceof ApiError ? t("invalidCredentials") : t("somethingWentWrong"));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", padding: "var(--space-4)" }}>
      <div style={{ position: "fixed", top: 20, right: 20, display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
        <ThemeSelect className="theme-select" />
        <LanguageSwitch />
      </div>
      <div
        className="card"
        style={{ width: "100%", maxWidth: 360, padding: "var(--space-8) var(--space-7)", boxShadow: "var(--shadow-md)" }}
      >
        {/* Also the way back to the public page — without it, anyone who reaches /login by
            mistake has no route out except the browser's back button. */}
        <Link
          to="/"
          style={{ display: "flex", justifyContent: "center", marginBottom: "var(--space-6)", textDecoration: "none" }}
        >
          <Logo />
        </Link>
        <h1 style={{ fontSize: "var(--text-lg)", textAlign: "center", marginBottom: "var(--space-5)" }}>{t("signIn")}</h1>
        <form onSubmit={handleSubmit}>
          <TextField
            label={t("email")}
            type="text"
            placeholder="you@business.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
          />
          <TextField
            label={t("password")}
            type="password"
            placeholder="••••••••"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
          />
          {error && <div className="field-error" style={{ marginBottom: "var(--space-4)" }}>{error}</div>}
          <Button type="submit" style={{ width: "100%" }} loading={submitting}>
            {t("signIn")}
          </Button>
        </form>
        {/* The logo above links home too, but only someone who has already learned that a
            logo is clickable will try it. This says so. */}
        <div style={{ textAlign: "center", marginTop: "var(--space-5)" }}>
          <Link to="/" className="quiet-link">
            ← {t("backToHome")}
          </Link>
        </div>
      </div>
    </div>
  );
}
