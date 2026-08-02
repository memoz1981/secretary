import { test, expect } from "@playwright/test";
import { loginAsStaff } from "./helpers";

test("dashboard loads KPI summary without error", async ({ page }) => {
  await loginAsStaff(page);
  await page.goto("/dashboard");

  await expect(page.getByText("KPI Dashboard")).toBeVisible();
  await expect(page.getByText("Total calls")).toBeVisible();
  await expect(page.getByText("Escalation outcomes")).toBeVisible();
});
