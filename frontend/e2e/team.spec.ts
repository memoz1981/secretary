import { test, expect } from "@playwright/test";
import { loginAsStaff } from "./helpers";

// Assumes E2E_STAFF_EMAIL belongs to an Owner account — Team is Owner-only.
test("team page loads providers and staff sections without error", async ({ page }) => {
  await loginAsStaff(page);
  await page.goto("/team");

  await expect(page.getByText("Providers")).toBeVisible();
  await expect(page.getByText("Staff accounts")).toBeVisible();
});
