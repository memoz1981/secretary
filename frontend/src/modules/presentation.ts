import type { Module } from "@/shared/api/types";
import type { TranslationKey } from "@/shared/i18n/translations";

/** How each module looks, whether or not it is built.
 *
 * Separate from the manifest on purpose. A manifest describes a module that exists — routes,
 * nav, a home page — and only two do. This describes every module the platform sells, so the
 * picker can show a tenant what else there is beside what they hold.
 *
 * The icons and titles are the landing page's, deliberately: a customer sees these tiles when
 * they are deciding to buy and again when they sign in, and they should be recognisably the
 * same things. */
export interface ModulePresentation {
  titleKey: TranslationKey;
  icon: string;
  tone: number;
}

export const MODULE_PRESENTATION: Record<Module, ModulePresentation> = {
  Appointment: { titleKey: "moduleAppointmentsTitle", icon: "calendarCheck", tone: 2 },
  Information: { titleKey: "moduleInfoTitle", icon: "info", tone: 1 },
  Reminder: { titleKey: "moduleRemindersTitle", icon: "bell", tone: 3 },
  Feedback: { titleKey: "moduleFeedbackTitle", icon: "star", tone: 6 },
  Order: { titleKey: "moduleOrdersTitle", icon: "bag", tone: 4 },
  Survey: { titleKey: "moduleSurveysTitle", icon: "clipboard", tone: 5 },
};
