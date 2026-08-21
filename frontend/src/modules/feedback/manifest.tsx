import { Route } from "react-router-dom";
import { RequireRole } from "@/shared/auth/RequireRole";
import { RequireModule } from "@/shared/auth/RequireModule";
import { QuestionnairesPage } from "@/modules/feedback/pages/Questionnaires";
import { NewFeedbackCallPage } from "@/modules/feedback/pages/NewCall";
import { FeedbackCallsPage } from "@/modules/feedback/pages/FeedbackCalls";
import { FeedbackCallDetailPage } from "@/modules/feedback/pages/FeedbackCallDetail";
import { FeedbackDashboardPage } from "@/modules/feedback/pages/FeedbackDashboard";
import { FeedbackFollowUpPage } from "@/modules/feedback/pages/FollowUp";
import type { ModuleManifest } from "@/modules/registry";

/** Rəy və məmnuniyyət — the survey line.
 *
 * The only module whose calls start from a form. Questionnaires define what is asked, "New call"
 * says who to ask, and the dashboard and call list read the results back. The call page is
 * standing in for a scheduler: it is where a campaign will one day sit, and the shape of the rest
 * does not change when it does.
 *
 * Gemini only, refused server-side rather than merely absent from a picker. */
export const feedbackModule: ModuleManifest = {
  key: "Feedback",
  pathPrefix: "/feedback",
  home: "/feedback/dashboard",
  titleKey: "moduleFeedbackTitle",
  descriptionKey: "moduleFeedbackText",

  nav: (role, t) => [
    { label: t("navFeedbackDashboard"), to: "/feedback/dashboard" },
    { label: t("questionnaires"), to: "/feedback/questionnaires" },
    { label: t("feedbackCalls"), to: "/feedback/calls" },
    { label: t("followUp"), to: "/feedback/follow-up" },
    ...(role === "Owner" ? [{ label: t("newFeedbackCall"), to: "/feedback/new-call" }] : []),
  ],

  routes: (
    <Route path="/feedback">
      <Route
        path="dashboard"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Feedback">
              <FeedbackDashboardPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="questionnaires"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Feedback">
              <QuestionnairesPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="calls"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Feedback">
              <FeedbackCallsPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="calls/:id"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Feedback">
              <FeedbackCallDetailPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="follow-up"
        element={
          <RequireRole roles={["Owner", "Staff"]}>
            <RequireModule module="Feedback">
              <FeedbackFollowUpPage />
            </RequireModule>
          </RequireRole>
        }
      />
      <Route
        path="new-call"
        element={
          <RequireRole roles={["Owner"]}>
            <RequireModule module="Feedback">
              <NewFeedbackCallPage />
            </RequireModule>
          </RequireRole>
        }
      />
    </Route>
  ),
};
