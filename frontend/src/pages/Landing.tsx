import { Link } from "react-router-dom";
import { Logo } from "@/shared/components/Logo";
import { IconChip, type IconName } from "@/shared/components/LandingArt";
import { Card } from "@/shared/components/Card";
import { Pill } from "@/shared/components/Pill";
import { ThemeSelect } from "@/shared/components/ThemeSelect";
import { LanguageSwitch } from "@/shared/components/LanguageSwitch";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import type { TranslationKey } from "@/shared/i18n/translations";

/** The public front door — the only page in the app that speaks to someone who is not a
 *  customer yet, and the only one outside AppShell. Login lives at /login as before; this page
 *  routes to it rather than embedding a second copy of the form.
 *
 *  Every section uses the same three-column grid so the cards are one size throughout: a row
 *  of wide boxes next to a row of narrow ones reads as two unrelated pages stitched together.
 */

interface Item {
  title: TranslationKey;
  text: TranslationKey;
  icon: IconName;
  tone: number;
}

const STEPS: Item[] = [
  { title: "landingStep1Title", text: "landingStep1Text", icon: "phoneBoth", tone: 1 },
  { title: "landingStep2Title", text: "landingStep2Text", icon: "speak", tone: 2 },
  { title: "landingStep3Title", text: "landingStep3Text", icon: "calendarCheck", tone: 3 },
];

/** `live` is a claim about working software, not a position on a roadmap: a module is listed as
 *  active when somebody could use it today, and as coming when it is a plan. Appointments, orders
 *  and feedback have all taken real calls; the other three have not.
 *
 *  Sorted so the working ones come first. A visitor reading top to bottom should meet what exists
 *  before what is promised — a grid that opens with two "soon" cards reads as a product that is
 *  mostly intention. */
const MODULES: (Item & { live: boolean })[] = [
  { title: "moduleAppointmentsTitle", text: "moduleAppointmentsText", icon: "calendarCheck", tone: 2, live: true },
  { title: "moduleInfoTitle", text: "moduleInfoText", icon: "info", tone: 1, live: false },
  { title: "moduleRemindersTitle", text: "moduleRemindersText", icon: "bell", tone: 3, live: false },
  { title: "moduleFeedbackTitle", text: "moduleFeedbackText", icon: "star", tone: 6, live: true },
  { title: "moduleOrdersTitle", text: "moduleOrdersText", icon: "bag", tone: 4, live: true },
  { title: "moduleSurveysTitle", text: "moduleSurveysText", icon: "clipboard", tone: 5, live: false },
];

/** Working modules first, each group keeping the order above — that order groups them by the kind
 *  of work they do, and sorting is not a reason to lose it. */
const LISTED_MODULES = [...MODULES].sort((a, b) => Number(b.live) - Number(a.live));

const REASONS: Item[] = [
  { title: "landingWhy1Title", text: "landingWhy1Text", icon: "globe", tone: 1 },
  { title: "landingWhy2Title", text: "landingWhy2Text", icon: "clock", tone: 3 },
  { title: "landingWhy3Title", text: "landingWhy3Text", icon: "transcript", tone: 5 },
  { title: "landingWhy4Title", text: "landingWhy4Text", icon: "gauge", tone: 2 },
  { title: "landingWhy5Title", text: "landingWhy5Text", icon: "handoff", tone: 6 },
  { title: "landingWhy6Title", text: "landingWhy6Text", icon: "sync", tone: 7 },
];

export function LandingPage() {
  const { t } = useLanguage();

  return (
    <div className="landing">
      <header className="landing-header">
        <Logo />
        <div className="landing-controls">
          <ThemeSelect className="theme-select" />
          <LanguageSwitch />
          <Link to="/login" className="btn">
            {t("signIn")}
          </Link>
        </div>
      </header>

      <main className="landing-main">
        <section className="hero">
          <p className="hero-motto">{t("landingMotto")}</p>
          <h1>{t("landingHeroTitle")}</h1>
          <p>{t("landingHeroLead")}</p>
        </section>

        <section className="landing-section">
          <h2>{t("landingHowTitle")}</h2>
          <div className="card-grid">
            {STEPS.map((step) => (
              <Card key={step.title} className="landing-card">
                <IconChip name={step.icon} tone={step.tone} />
                <h3>{t(step.title)}</h3>
                <p>{t(step.text)}</p>
              </Card>
            ))}
          </div>
        </section>

        <section className="landing-section">
          <h2>{t("landingModulesTitle")}</h2>
          <p className="lead">{t("landingModulesLead")}</p>
          <div className="card-grid">
            {LISTED_MODULES.map((module) => (
              <Card key={module.title} className="landing-card">
                <div className="module-head">
                  <IconChip name={module.icon} tone={module.tone} />
                  <Pill variant={module.live ? "success" : "neutral"}>
                    {module.live ? t("landingStatusActive") : t("landingStatusSoon")}
                  </Pill>
                </div>
                <h3>{t(module.title)}</h3>
                <p>{t(module.text)}</p>
              </Card>
            ))}
          </div>
        </section>

        <section className="landing-section">
          <h2>{t("landingWhyTitle")}</h2>
          <div className="card-grid">
            {REASONS.map((reason) => (
              <Card key={reason.title} className="landing-card">
                <IconChip name={reason.icon} tone={reason.tone} />
                <h3>{t(reason.title)}</h3>
                <p>{t(reason.text)}</p>
              </Card>
            ))}
          </div>
        </section>
      </main>

      <footer className="landing-footer">
        <div className="landing-footer-brand">
          <Logo size={24} />
          <span>{t("landingFooterTagline")}</span>
        </div>
        <span>
          © {new Date().getFullYear()} sekretar.az · {t("landingFooterRights")}
        </span>
      </footer>
    </div>
  );
}
